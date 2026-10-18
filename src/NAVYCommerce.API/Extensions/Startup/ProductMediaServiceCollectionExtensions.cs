using System.Text.RegularExpressions;
using NAVYCommerce.API.BackgroundServices;
using NAVYCommerce.API.Services;
using NAVYCommerce.Application.Abstractions.Imaging;
using NAVYCommerce.Application.Abstractions.Storage;
using NAVYCommerce.Infrastructure.Imaging;
using NAVYCommerce.Infrastructure.Storage;

namespace NAVYCommerce.API.Extensions.Startup;

public static class ProductMediaServiceCollectionExtensions
{
    public static IServiceCollection AddNavyProductMedia(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ProductImageStorageOptions>()
            .Bind(configuration.GetSection(ProductImageStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Connection string storage wajib diisi.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName)
                && options.ContainerName.Length >= 3
                && options.ContainerName.Length <= 63
                && Regex.IsMatch(options.ContainerName, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z"),
                "Nama container storage tidak valid.")
            .Validate(options => Uri.TryCreate(options.PublicServiceUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps
                    || (options.AllowHttp && uri.Scheme == Uri.UriSchemeHttp))
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && string.IsNullOrEmpty(uri.UserInfo),
                "URL storage tidak valid.")
            .Validate(options => options.ReadUrlMinutes >= 5 && options.ReadUrlMinutes <= 60,
                "Masa berlaku URL gambar harus 5–60 menit.")
            .Validate(options => options.MaxFileBytes > 0 && options.MaxFileBytes <= 5 * 1024 * 1024,
                "Batas ukuran gambar maksimal 5 MiB.")
            .Validate(options => !options.AllowHttp || environment.IsDevelopment(),
                "HTTP storage hanya diizinkan untuk development.")
            .ValidateOnStart();

        services.AddSingleton<IProductImageStorage, AzureBlobProductImageStorage>();
        services.AddSingleton<IProductImageProcessor, ImageSharpProductImageProcessor>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddHostedService<StorageDeletionWorker>();
        return services;
    }
}
