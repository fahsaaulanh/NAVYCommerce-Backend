using Microsoft.Extensions.DependencyInjection.Extensions;
using NAVYCommerce.API.Extensions;
using NAVYCommerce.API.Services;
using NAVYCommerce.API.Services.Addresses;
using NAVYCommerce.API.Services.Carts;
using NAVYCommerce.API.Services.Checkout.Shipping;
using NAVYCommerce.API.Services.Shipping.Locations;
using NAVYCommerce.API.Services.Stores.ShippingOrigins;

namespace NAVYCommerce.API.Extensions.Startup;

public static class CommerceServiceCollectionExtensions
{
    public static IServiceCollection AddNavyCommerce(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Memanggil extension yang sudah ada, bukan membuat provider kedua.
        services.AddRegionServices(configuration);
        services.AddShippingServices(configuration);

        // Tetap memakai registrasi lama bila extension shipping sudah menambahkannya.
        services.TryAddSingleton<ShippingLocationResolutionCoordinator>();
        services.TryAddScoped<IShippingLocationService, ShippingLocationService>();
        services.AddScoped<IRegistrationOtpService, RegistrationOtpService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IStoreShippingOriginService, StoreShippingOriginService>();
        services.AddScoped<IProductVariantFactory, ProductVariantFactory>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICheckoutShippingService, CheckoutShippingService>();
        return services;
    }
}
