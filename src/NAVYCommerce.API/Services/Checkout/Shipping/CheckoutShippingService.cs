using Microsoft.EntityFrameworkCore;
using NAVYCommerce.API.Contracts.Checkout;
using NAVYCommerce.API.Services.Carts;
using NAVYCommerce.API.Services.Shipping.Locations;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Services.Checkout.Shipping;

public sealed class CheckoutShippingService : ICheckoutShippingService
{
    // Batas awal demo, bukan pernyataan batas layanan semua kurir.
    private const int MaxStores = 5;
    private const int MaxPackageWeightGrams = 30_000;
    private readonly ApplicationDbContext _db;
    private readonly IShippingLocationService _locations;
    private readonly IRajaOngkirClient _client;
    private readonly ILogger<CheckoutShippingService> _logger;

    public CheckoutShippingService(ApplicationDbContext db,
        IShippingLocationService locations, IRajaOngkirClient client,
        ILogger<CheckoutShippingService> logger)
    {
        _db = db;
        _locations = locations;
        _client = client;
        _logger = logger;
    }

    public async Task<CheckoutShippingResult> CalculateAsync(
        Guid customerId, CheckoutShippingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty || !await _db.Users.AnyAsync(user =>
                user.Id == customerId && user.IsVerified &&
                user.Role == UserRole.Customer, cancellationToken))
            return new(CheckoutShippingStatus.CustomerUnavailable);

        // Validasi ulang batas, termasuk bila service dipanggil di luar controller.
        if (request.CartItemIds == null || request.CartItemIds.Length is < 1 or > 100 ||
            request.CartItemIds.Any(id => id == Guid.Empty) ||
            request.CartItemIds.Distinct().Count() != request.CartItemIds.Length)
            return new(CheckoutShippingStatus.InvalidCartItems);

        var address = await _db.Set<CustomerAddress>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == request.AddressId &&
                value.CustomerId == customerId, cancellationToken);
        if (address == null) return new(CheckoutShippingStatus.AddressNotFound);

        // Tidak mengambil gambar atau membuat signed URL hanya untuk menghitung berat.
        var items = await _db.CartItems.AsNoTrackingWithIdentityResolution()
            .Where(item => item.Cart.CustomerId == customerId &&
                request.CartItemIds.Contains(item.Id))
            .Include(item => item.ProductVariant)
                .ThenInclude(variant => variant.Product!)
                .ThenInclude(product => product.Store)
                .ThenInclude(store => store.Owner)
            .ToListAsync(cancellationToken);

        var foundIds = items.Select(item => item.Id).ToHashSet();
        var invalidIds = request.CartItemIds.Where(id => !foundIds.Contains(id))
            .Concat(items.Where(item =>
                    CartVariantRules.Validate(item.ProductVariant, item.Quantity) != null)
                .Select(item => item.Id))
            .Distinct().ToArray();
        if (invalidIds.Length > 0)
            return new(CheckoutShippingStatus.InvalidCartItems,
                InvalidCartItemIds: invalidIds);

        var groups = items.GroupBy(item => item.ProductVariant.Product!.StoreId)
            .OrderBy(group => group.Key).ToArray();
        if (groups.Length > MaxStores)
            return new(CheckoutShippingStatus.TooManyStores);

        // Tidak menerima ID provider dari browser: backend menyelesaikan mapping sendiri.
        var destination = await _locations.ResolveAsync(
            ShippingRegion.FromCustomerAddress(address), cancellationToken);
        if (!destination.IsResolved)
            return new(CheckoutShippingStatus.DestinationNotMapped, LocationFailure: destination);
        var destinationId = destination.DestinationId!.Value;

        var storeIds = groups.Select(group => group.Key).ToArray();
        var origins = await _db.Set<StoreShippingOrigin>().AsNoTracking()
            .Where(origin => storeIds.Contains(origin.StoreId))
            .ToDictionaryAsync(origin => origin.StoreId, cancellationToken);
        var packages = new List<CheckoutShippingPackageResponse>();

        foreach (var group in groups)
        {
            var store = group.First().ProductVariant.Product!.Store;
            var ids = group.Select(item => item.Id).OrderBy(id => id).ToArray();
            var subtotal = group.Sum(item =>
                (item.ProductVariant.Product!.BasePrice +
                 item.ProductVariant.PriceAdjustment) * item.Quantity);
            long weight = 0;
            var invalidWeight = false;
            try
            {
                foreach (var item in group)
                {
                    var unitWeight = item.ProductVariant.Product!.WeightGrams;
                    if (unitWeight <= 0) { invalidWeight = true; break; }
                    weight = checked(weight + (long)unitWeight * item.Quantity);
                }
            }
            catch (OverflowException) { invalidWeight = true; }

            var grams = !invalidWeight && weight > 0 && weight <= MaxPackageWeightGrams
                ? (int)weight : 0;
            CheckoutShippingPackageResponse Package(string status, string message) =>
                new(store.Id, store.Name, ids, grams, subtotal,
                    status, message, Array.Empty<ShippingRate>());

            if (grams == 0)
            {
                packages.Add(Package("WEIGHT_INVALID",
                    "Berat produk belum valid atau total paket melebihi batas demo 30 kg."));
                continue;
            }
            if (!origins.TryGetValue(store.Id, out var origin))
            {
                packages.Add(Package("ORIGIN_MISSING", "Alamat asal pengiriman toko belum diatur."));
                continue;
            }
            var originLocation = await _locations.ResolveAsync(
                ShippingRegion.FromStoreOrigin(origin), cancellationToken);
            if (!originLocation.IsResolved)
            {
                packages.Add(Package("ORIGIN_" + originLocation.Code,
                    "Alamat asal toko: " + originLocation.Message));
                continue;
            }
            var originId = originLocation.DestinationId!.Value;

            try
            {
                // Berurutan: membatasi lonjakan request provider dan penggunaan DbContext.
                var rates = await _client.GetRatesAsync(originId,
                    destinationId, grams, cancellationToken);
                packages.Add(new(store.Id, store.Name, ids, grams, subtotal,
                    rates.Count > 0 ? "READY" : "NO_SERVICES",
                    rates.Count > 0 ? null : "Belum ada layanan pengiriman untuk paket ini.",
                    rates));
            }
            catch (ShippingProviderException exception)
            {
                _logger.LogWarning(exception, "Gagal mengambil ongkir toko {StoreId}.", store.Id);
                packages.Add(Package("PROVIDER_UNAVAILABLE",
                    "Pilihan pengiriman belum dapat dimuat. Silakan coba kembali."));
            }
        }

        return new(CheckoutShippingStatus.Calculated,
            new(request.AddressId, DateTime.UtcNow,
                packages.Count > 0 && packages.All(package => package.Status == "READY"),
                packages));
    }
}
