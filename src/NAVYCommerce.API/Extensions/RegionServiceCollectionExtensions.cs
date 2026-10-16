using System;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NAVYCommerce.API.Services.Regions;

namespace NAVYCommerce.API.Extensions;

public static class RegionServiceCollectionExtensions
{
  public static IServiceCollection AddRegionServices(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    var baseUrl = configuration["Regions:Emsifa:BaseUrl"]
        ?? "https://www.emsifa.com/api-wilayah-indonesia/v2/";

    if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute,
        out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(baseUri.Query) ||
        !string.IsNullOrEmpty(baseUri.Fragment) ||
        !string.IsNullOrEmpty(baseUri.UserInfo))
    {
      throw new InvalidOperationException(
          "Regions:Emsifa:BaseUrl harus berupa URL HTTPS tanpa query, fragment, atau credential.");
    }

    services.AddHttpClient(EmsifaRegionService.HttpClientName, client =>
    {
      client.BaseAddress = baseUri;
      client.Timeout = TimeSpan.FromSeconds(8);
      client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
      client.DefaultRequestHeaders.Accept.Add(
          new MediaTypeWithQualityHeaderValue("application/json"));
    });

    // Singleton diperlukan agar cache dan lock dibagikan antar request.
    // Service ini tidak menyimpan DbContext atau HttpClient berumur panjang.
    services.AddSingleton<IRegionService, EmsifaRegionService>();

    return services;
  }
}
