using NAVYCommerce.API.Services.Shipping.Locations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NAVYCommerce.API.Services.Shipping.RajaOngkir;
using System.Threading.RateLimiting;

namespace NAVYCommerce.API.Extensions;

public static class ShippingServiceCollectionExtensions
{
    public static IServiceCollection AddShippingServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RajaOngkirOptions>()
            .Bind(configuration.GetSection(RajaOngkirOptions.SectionName))
            .Validate(o => o.BaseUrl == "https://rajaongkir.komerce.id/api/v1/",
                "Base URL RajaOngkir harus https://rajaongkir.komerce.id/api/v1/.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                "API key RajaOngkir wajib diisi pada Shipping:RajaOngkir:ApiKey.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddScoped<IShippingLocationService, ShippingLocationService>();
        services.AddHttpClient<IRajaOngkirClient, RajaOngkirClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<RajaOngkirOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(15);
        }).RedactLoggedHeaders(new[] { "key" });


        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("shipping-development", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });
        return services;
    }
}
