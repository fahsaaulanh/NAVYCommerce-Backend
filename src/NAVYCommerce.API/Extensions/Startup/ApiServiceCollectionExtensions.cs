using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace NAVYCommerce.API.Extensions.Startup;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddNavyApi(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        AddSwagger(services);
        AddJwt(services, configuration);
        AddFrontendCors(services, configuration, environment);
        AddCheckoutRateLimit(services);
        return services;
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NAVYCommerce API", Version = "v1"
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Masukkan token JWT saja, tanpa awalan Bearer."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme, Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
        });
    }

    private static void AddJwt(IServiceCollection services, IConfiguration configuration)
    {
        var secret = Require(configuration, "JwtSettings:Secret");
        var issuer = Require(configuration, "JwtSettings:Issuer");
        var audience = Require(configuration, "JwtSettings:Audience");
        var signingKey = Encoding.UTF8.GetBytes(secret);
        if (signingKey.Length < 32)
            throw new InvalidOperationException("JwtSettings:Secret minimal 32 byte untuk HS256.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                    RoleClaimType = "Role"
                };
            });
        services.AddAuthorization();
    }

    private static void AddFrontendCors(IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins == null || origins.Length == 0)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Cors:AllowedOrigins wajib diisi di luar Development.");
            origins = new[] { "http://localhost:3000", "http://127.0.0.1:3000" };
        }

        var allowed = origins.Select(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin) ||
                !Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.Host.Contains('*'))
                throw new InvalidOperationException("Cors:AllowedOrigins harus berisi origin HTTP/HTTPS tanpa path atau wildcard.");
            return uri.GetLeftPart(UriPartial.Authority);
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        services.AddCors(options => options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod()));
    }

    private static void AddCheckoutRateLimit(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("checkout-shipping", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey:
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub")
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Konfigurasi {key} wajib diisi.");
        return value;
    }
}
