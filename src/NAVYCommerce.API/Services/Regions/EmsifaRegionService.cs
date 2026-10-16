using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NAVYCommerce.API.Contracts.Regions;

namespace NAVYCommerce.API.Services.Regions;

public sealed class EmsifaRegionService : IRegionService, IDisposable
{
  public const string HttpClientName = "Regions.EmsifaV2";

  private static readonly TimeSpan FreshDuration = TimeSpan.FromHours(24);
  private static readonly TimeSpan RetentionDuration = TimeSpan.FromDays(7);
  private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(1);

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<EmsifaRegionService> _logger;

  private readonly MemoryCache _cache = new(new MemoryCacheOptions
  {
    SizeLimit = 256
  });

  private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 32)
      .Select(_ => new SemaphoreSlim(1, 1))
      .ToArray();

  public EmsifaRegionService(
      IHttpClientFactory httpClientFactory,
      ILogger<EmsifaRegionService> logger)
  {
    _httpClientFactory = httpClientFactory;
    _logger = logger;
  }

  public Task<IReadOnlyList<RegionResponse>> GetProvincesAsync(
      CancellationToken cancellationToken = default)
      => GetListAsync("provinces.json", RegionCodePatterns.Province, null,
          cancellationToken);

  public Task<IReadOnlyList<RegionResponse>> GetCitiesAsync(
      string provinceCode,
      CancellationToken cancellationToken = default)
  {
    EnsureCode(provinceCode, RegionCodePatterns.Province);
    return GetListAsync($"regencies/{provinceCode}.json",
        RegionCodePatterns.City, provinceCode, cancellationToken);
  }

  public Task<IReadOnlyList<RegionResponse>> GetDistrictsAsync(
      string cityCode,
      CancellationToken cancellationToken = default)
  {
    EnsureCode(cityCode, RegionCodePatterns.City);
    return GetListAsync($"districts/{cityCode}.json",
        RegionCodePatterns.District, cityCode, cancellationToken);
  }

  public Task<IReadOnlyList<RegionResponse>> GetVillagesAsync(
      string districtCode,
      CancellationToken cancellationToken = default)
  {
    EnsureCode(districtCode, RegionCodePatterns.District);
    return GetListAsync($"villages/{districtCode}.json",
        RegionCodePatterns.Village, districtCode, cancellationToken);
  }

  public async Task<ResolvedRegion?> ResolveVillageAsync(
      string villageCode,
      CancellationToken cancellationToken = default)
  {
    if (!Matches(villageCode, RegionCodePatterns.Village)) return null;

    var village = await ReadCachedAsync<EmsifaRegion>(
        $"villages/{villageCode}.json",
        value => IsValidVillage(value, villageCode),
        cancellationToken);

    if (village == null) return null;

    return new ResolvedRegion(
        Map(village.Province!),
        Map(village.Regency!),
        Map(village.District!),
        Map(village));
  }

  private async Task<IReadOnlyList<RegionResponse>> GetListAsync(
      string path,
      string itemPattern,
      string? parentCode,
      CancellationToken cancellationToken)
  {
    var rows = await ReadCachedAsync<List<EmsifaRegion>>(
        path,
        values => values.All(value => IsValidNode(value, itemPattern) &&
            (parentCode == null || IsChildOf(value, parentCode))) &&
            values.Select(value => value.Id).Distinct().Count() == values.Count,
        cancellationToken);

    if (rows == null) return Array.Empty<RegionResponse>();

    return rows.Select(Map)
        .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
  }

  private async Task<T?> ReadCachedAsync<T>(
      string path,
      Func<T, bool> isValid,
      CancellationToken cancellationToken) where T : class
  {
    var key = $"emsifa-v2:{path}";
    if (_cache.TryGetValue<CachedData<T>>(key, out var cached) &&
        cached != null && cached.RefreshAt > DateTimeOffset.UtcNow)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return cached.Value;
    }

    var gate = _locks[(int)((uint)StringComparer.Ordinal.GetHashCode(key)
        % (uint)_locks.Length)];

    await gate.WaitAsync(cancellationToken);
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      _cache.TryGetValue<CachedData<T>>(key, out cached);
      if (cached != null && cached.RefreshAt > DateTimeOffset.UtcNow)
        return cached.Value;

      try
      {
        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {

          _cache.Remove(key);
          return null;
        }

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(
            cancellationToken: cancellationToken);

        var data = envelope?.Data;
        if (data == null || !isValid(data))
          throw new JsonException("Struktur data wilayah provider tidak valid.");

        var now = DateTimeOffset.UtcNow;
        Put(key, new CachedData<T>(data,
            now.Add(FreshDuration), now.Add(RetentionDuration)));

        return data;
      }
      catch (OperationCanceledException)
          when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception) when (
          exception is HttpRequestException ||
          exception is OperationCanceledException ||
          exception is JsonException)
      {
        var now = DateTimeOffset.UtcNow;
        if (cached != null && cached.ExpiresAt > now)
        {
          _logger.LogWarning(exception,
              "Provider wilayah gagal pada {Path}; memakai cache cadangan.", path);


          Put(key, cached with { RefreshAt = now.Add(FailureCooldown) });
          return cached.Value;
        }

        _logger.LogWarning(exception,
            "Provider wilayah gagal pada {Path}; cache tidak tersedia.", path);
        throw new RegionServiceUnavailableException(exception);
      }
    }
    finally
    {
      gate.Release();
    }
  }

  private void Put<T>(string key, CachedData<T> entry) where T : class
      => _cache.Set(key, entry, new MemoryCacheEntryOptions
      {
        AbsoluteExpiration = entry.ExpiresAt,
        Size = 1
      });

  private static bool IsValidVillage(EmsifaRegion village, string expectedCode)
      => IsValidNode(village, RegionCodePatterns.Village) &&
         village.Id == expectedCode &&
         IsValidNode(village.Province, RegionCodePatterns.Province) &&
         IsValidNode(village.Regency, RegionCodePatterns.City) &&
         IsValidNode(village.District, RegionCodePatterns.District) &&
         IsChildOf(village.Regency!, village.Province!.Id) &&
         IsChildOf(village.District!, village.Regency!.Id) &&
         IsChildOf(village, village.District!.Id);

  private static bool IsValidNode(EmsifaRegion? value, string pattern)
      => value != null && Matches(value.Id, pattern) &&
         !string.IsNullOrWhiteSpace(value.Name) && value.Name.Trim().Length <= 100 &&
         (string.IsNullOrWhiteSpace(value.PostalCode) || Matches(value.PostalCode, @"^[0-9]{5}$"));

  private static bool IsChildOf(EmsifaRegion value, string parentCode)
      => value.Id.StartsWith(parentCode + ".", StringComparison.Ordinal);

  private static bool Matches(string? value, string pattern)
  {
    if (value == null) return false;
    var match = Regex.Match(value, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    return match.Success && match.Length == value.Length;
  }

  private static void EnsureCode(string code, string pattern)
  {
    if (!Matches(code, pattern))
      throw new ArgumentException("Format kode wilayah tidak valid.", nameof(code));
  }

  private static RegionResponse Map(EmsifaRegion value)
      => new(value.Id, value.Name.Trim(),
          string.IsNullOrWhiteSpace(value.PostalCode) ? null : value.PostalCode);

  public void Dispose() => _cache.Dispose();

  private sealed record CachedData<T>(
      T Value, DateTimeOffset RefreshAt, DateTimeOffset ExpiresAt);


  private sealed class Envelope<T>
  {
    public Envelope() { }

    public T? Data { get; init; }
  }

  private sealed class EmsifaRegion
  {
    public EmsifaRegion() { }

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }

    public EmsifaRegion? Province { get; init; }
    public EmsifaRegion? Regency { get; init; }
    public EmsifaRegion? District { get; init; }
  }
}
