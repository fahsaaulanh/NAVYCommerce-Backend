using Microsoft.Extensions.Caching.Memory;

namespace NAVYCommerce.API.Services.Shipping.Locations;

// Singleton khusus: ukuran cache tidak membatasi IMemoryCache milik fitur lain.
public sealed class ShippingLocationResolutionCoordinator : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 32)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public SemaphoreSlim GetGate(string fingerprint) =>
        _gates[Convert.ToInt32(fingerprint[..2], 16) % _gates.Length];

    public ShippingLocationResolution? GetFailure(string key) =>
        _cache.TryGetValue(key, out ShippingLocationResolution? result) ? result : null;

    public void RememberFailure(string key, ShippingLocationResolution result)
    {
        if (result.IsResolved || result.Status == ShippingLocationResolutionStatus.RequestBudgetExceeded)
            return;
        var seconds = result.Status == ShippingLocationResolutionStatus.ProviderUnavailable ? 15 : 60;
        _cache.Set(key, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds)
        });
    }

    public void Forget(string key) => _cache.Remove(key);

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var gate in _gates) gate.Dispose();
    }
}
