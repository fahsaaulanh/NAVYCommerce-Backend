using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace NAVYCommerce.API.Services.Shipping.RajaOngkir;

public sealed class RajaOngkirClient : IRajaOngkirClient
{
    private const string Couriers = "jne:jnt:sicepat";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public RajaOngkirClient(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ShippingDestination>> SearchDestinationsAsync(
        string search, int limit = 20, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        search = (search ?? string.Empty).Trim();
        if (search.Length is < 3 or > 100)
            throw new ArgumentException("Kata pencarian harus 3–100 karakter.", nameof(search));
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit harus 1–50.");
        if (offset is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset harus 0–10000.");

        var cacheKey = $"rajaongkir:destinations:{search}:{limit}:{offset}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ShippingDestination>? cached)
            && cached is not null)
            return cached;

        var path = "destination/domestic-destination"
            + $"?search={Uri.EscapeDataString(search)}&limit={limit}&offset={offset}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var data = await SendAsync<DestinationDto>(request, true, cancellationToken);
        if (data.Any(x => x.Id <= 0 || string.IsNullOrWhiteSpace(x.Label)))
            throw InvalidResponse();

        IReadOnlyList<ShippingDestination> result = Array.AsReadOnly(data.Select(x =>
            new ShippingDestination(x.Id, x.Label!, x.Province ?? string.Empty,
                x.City ?? string.Empty, x.District ?? string.Empty,
                x.Village ?? string.Empty, x.PostalCode ?? string.Empty)).ToArray());

        _cache.Set(cacheKey, result, TimeSpan.FromHours(12));
        return result;
    }

    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        int originId, int destinationId, int weightGrams,
        CancellationToken cancellationToken = default)
    {
        if (originId <= 0) throw new ArgumentOutOfRangeException(nameof(originId));
        if (destinationId <= 0) throw new ArgumentOutOfRangeException(nameof(destinationId));
        if (weightGrams <= 0) throw new ArgumentOutOfRangeException(nameof(weightGrams));

        var cacheKey = $"rajaongkir:rates:{originId}:{destinationId}:{weightGrams}:{Couriers}:lowest";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ShippingRate>? cached)
            && cached is not null)
            return cached;

        using var request = new HttpRequestMessage(HttpMethod.Post, "calculate/domestic-cost")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["origin"] = originId.ToString(CultureInfo.InvariantCulture),
                ["destination"] = destinationId.ToString(CultureInfo.InvariantCulture),
                ["weight"] = weightGrams.ToString(CultureInfo.InvariantCulture),
                ["courier"] = Couriers,
                ["price"] = "lowest"
            })
        };
        var data = await SendAsync<RateDto>(request, false, cancellationToken);
        if (data.Any(x => string.IsNullOrWhiteSpace(x.Code)
            || string.IsNullOrWhiteSpace(x.Service) || x.Cost is null or < 0))
            throw InvalidResponse();

        IReadOnlyList<ShippingRate> result = Array.AsReadOnly(data.Select(x =>
            new ShippingRate(x.Code!, x.Name ?? x.Code!, x.Service!,
                x.Description ?? string.Empty, x.Cost!.Value,
                x.Etd ?? string.Empty)).OrderBy(x => x.Cost).ToArray());

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private async Task<List<T>> SendAsync<T>(HttpRequestMessage request,
        bool allowNotFound, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return new List<T>();
            if (!response.IsSuccessStatusCode)
                throw ProviderFailure((int)response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<Envelope<T>>(
                JsonOptions, cancellationToken);
            if (payload?.Meta is null) throw InvalidResponse();
            if (allowNotFound && payload.Meta.Code == 404)
                return new List<T>();
            if (payload.Meta.Code != 200)
                throw ProviderFailure(payload.Meta.Code);
            return payload.Data ?? throw InvalidResponse();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShippingProviderException("Layanan ongkir terlalu lama merespons. Coba lagi.", 504);
        }
        catch (HttpRequestException)
        {
            throw new ShippingProviderException("Layanan ongkir tidak dapat dihubungi. Coba lagi.", 502);
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    private static ShippingProviderException InvalidResponse() =>
        new("Respons layanan ongkir tidak valid.", 502);


    private static ShippingProviderException ProviderFailure(int code) => code switch
    {
        401 or 403 => new("Konfigurasi akses layanan ongkir perlu diperiksa.", 502),
        429 => new("Kuota atau batas permintaan layanan ongkir tercapai. Coba lagi nanti.", 503),
        _ => new("Layanan ongkir gagal memproses permintaan. Periksa lokasi, berat, dan ketersediaan layanan.", 502)
    };

    private sealed class Envelope<T>
    {
        public Envelope() { }
        public MetaDto? Meta { get; set; }
        public List<T>? Data { get; set; }
    }

    private sealed class MetaDto
    {
        public MetaDto() { }
        public int Code { get; set; }
    }

    private sealed class DestinationDto
    {
        public DestinationDto() { }
        public int Id { get; set; }
        public string? Label { get; set; }
        [JsonPropertyName("province_name")] public string? Province { get; set; }
        [JsonPropertyName("city_name")] public string? City { get; set; }
        [JsonPropertyName("district_name")] public string? District { get; set; }
        [JsonPropertyName("subdistrict_name")] public string? Village { get; set; }
        [JsonPropertyName("zip_code")] public string? PostalCode { get; set; }
    }

    private sealed class RateDto
    {
        public RateDto() { }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Service { get; set; }
        public string? Description { get; set; }
        public decimal? Cost { get; set; }
        public string? Etd { get; set; }
    }
}
