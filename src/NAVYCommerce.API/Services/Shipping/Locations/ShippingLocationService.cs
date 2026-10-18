using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using Npgsql;

namespace NAVYCommerce.API.Services.Shipping.Locations;

public sealed partial class ShippingLocationService : IShippingLocationService
{
    private const string Provider = "rajaongkir";
    private readonly ApplicationDbContext _db;
    private readonly IRajaOngkirClient _client;
    private readonly ShippingLocationResolutionCoordinator _coordinator;
    private readonly ILogger<ShippingLocationService> _logger;

    public ShippingLocationService(ApplicationDbContext db, IRajaOngkirClient client,
        ShippingLocationResolutionCoordinator coordinator, ILogger<ShippingLocationService> logger)
    {
        _db = db;
        _client = client;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<int?> FindConfirmedIdAsync(ShippingRegion region,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint(region);
        return await _db.Set<ShippingLocationMapping>().AsNoTracking()
            .Where(x => x.Provider == Provider && x.RegionFingerprint == fingerprint)
            .Select(x => (int?)x.DestinationId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShippingLocationCandidate>> FindCandidatesAsync(
        ShippingRegion region, int offset = 0, CancellationToken cancellationToken = default)
    {
        Validate(region);
        if (offset is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(offset));
        var destinations = await _client.SearchDestinationsAsync(
            region.PostalCode.Trim(), 50, offset, cancellationToken);
        return Array.AsReadOnly(destinations.Select(x =>
            new ShippingLocationCandidate(x, Matches(region, x))).ToArray());
    }

    public async Task<int> ConfirmAsync(ShippingRegion region, int destinationId,
        int searchOffset = 0, CancellationToken cancellationToken = default)
    {
        var fingerprint = Fingerprint(region);
        if (destinationId <= 0) throw new ArgumentOutOfRangeException(nameof(destinationId));
        var gate = _coordinator.GetGate(fingerprint);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existingId = await FindConfirmedIdAsync(region, cancellationToken);
            if (existingId.HasValue) return RequireSameMapping(existingId.Value, destinationId);


            var candidates = await FindCandidatesAsync(region, searchOffset, cancellationToken);
            var selected = candidates.SingleOrDefault(x => x.Destination.Id == destinationId);
            if (selected is null || !selected.MatchesAddress)
                throw new InvalidOperationException(
                    "Lokasi RajaOngkir belum cocok dengan seluruh wilayah dan kode pos alamat.");

            var id = await PersistMappingAsync(fingerprint, destinationId,
                selected.Destination.Label, cancellationToken);
            _coordinator.Forget(AutomaticCacheKey(fingerprint));
            return id;
        }
        finally { gate.Release(); }
    }

    private async Task<int> PersistMappingAsync(string fingerprint, int destinationId,
        string label, CancellationToken cancellationToken)
    {
        var mapping = new ShippingLocationMapping
        {
            Provider = Provider,
            RegionFingerprint = fingerprint,
            DestinationId = destinationId,
            DestinationLabel = label,
            ConfirmedAt = DateTime.UtcNow
        };
        _db.Set<ShippingLocationMapping>().Add(mapping);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, TableName: "shipping_location_mappings" })
        {
            _db.Entry(mapping).State = EntityState.Detached;
            var savedId = await _db.Set<ShippingLocationMapping>().AsNoTracking()
                .Where(x => x.Provider == Provider && x.RegionFingerprint == fingerprint)
                .Select(x => (int?)x.DestinationId).SingleOrDefaultAsync(cancellationToken);
            if (!savedId.HasValue) throw;
            return RequireSameMapping(savedId.Value, destinationId);
        }
        return destinationId;
    }

    private static int RequireSameMapping(int existingId, int requestedId)
    {
        if (existingId != requestedId) throw new MappingConflictException();
        return existingId;
    }

    private sealed class MappingConflictException : InvalidOperationException
    {
        public MappingConflictException() : base(
            "Wilayah ini sudah memiliki mapping berbeda. Perlu koreksi mapping secara eksplisit.")
        { }
    }

    private static bool Matches(ShippingRegion region, ShippingDestination destination)
    {
        if (destination is null || destination.Id <= 0) return false;
        var fields = new[] { destination.Province, destination.City, destination.District,
            destination.Village, destination.PostalCode };
        if (fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 200)) return false;
        return Normalize(region.Province) == Normalize(destination.Province)
            && CityMatches(region.City, destination.City)
            && Normalize(region.District) == Normalize(destination.District)
            && Normalize(region.Village) == Normalize(destination.Village)
            && Normalize(region.PostalCode) == Normalize(destination.PostalCode);
    }

    private static bool CityMatches(string first, string second)
    {
        var left = ParseCity(first);
        var right = ParseCity(second);
        return left.Name == right.Name &&
            (left.Kind is null || right.Kind is null || left.Kind == right.Kind);
    }

    private static (string Name, string? Kind) ParseCity(string value)
    {
        var normalized = Normalize(value);
        const string regencyPrefix = "KABUPATEN ";
        const string cityPrefix = "KOTA ";
        if (normalized.StartsWith(regencyPrefix, StringComparison.Ordinal))
            return (normalized[regencyPrefix.Length..], "KABUPATEN");
        if (normalized.StartsWith(cityPrefix, StringComparison.Ordinal))
            return (normalized[cityPrefix.Length..], "KOTA");
        return (normalized, null);
    }

    private static string Fingerprint(ShippingRegion region)
    {
        Validate(region);

        var canonical = JsonSerializer.Serialize(new[]
        {
            "v1", Normalize(region.VillageCode), Normalize(region.Province),
            Normalize(region.City), Normalize(region.District),
            Normalize(region.Village), Normalize(region.PostalCode)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static void Validate(ShippingRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var fields = new[] { region.VillageCode, region.Province, region.City,
            region.District, region.Village, region.PostalCode };
        if (fields.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
            throw new ArgumentException("Data wilayah alamat belum lengkap atau terlalu panjang.", nameof(region));
        if (region.PostalCode.Trim().Length != 5 ||
            region.PostalCode.Trim().Any(c => c < '0' || c > '9'))
            throw new ArgumentException("Kode pos harus terdiri dari 5 digit.", nameof(region));
    }
}
