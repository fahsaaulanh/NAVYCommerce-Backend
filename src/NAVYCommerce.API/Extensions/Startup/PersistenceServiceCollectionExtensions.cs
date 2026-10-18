using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Infrastructure.Data;
using Npgsql;

namespace NAVYCommerce.API.Extensions.Startup;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddNavyPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection wajib diisi.");

        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.EnableDynamicJson();
            return builder.Build();
        });
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        return services;
    }
}
