using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NAVYCommerce.Application.Abstractions.Storage;
using NAVYCommerce.Infrastructure.Storage;
using NAVYCommerce.Application.Abstractions.Imaging;
using NAVYCommerce.Infrastructure.Imaging;
using NAVYCommerce.API.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IVariantGeneratorService, VariantGeneratorService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dataSource));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!)),
            RoleClaimType = "Role"
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "NAVYCommerce API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Input your Bearer token in the format: Bearer {your token here}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddScoped<
    IRegistrationOtpService,
    RegistrationOtpService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IStoreService, StoreService>();

builder.Services.AddScoped<IProductVariantFactory, ProductVariantFactory>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services
    .AddOptions<ProductImageStorageOptions>()
    .Bind(builder.Configuration.GetSection(
        ProductImageStorageOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.ConnectionString),
        "Connection string storage wajib diisi.")
    .Validate(
        options =>
            options.ContainerName.Length >= 3
            && options.ContainerName.Length <= 63
            && System.Text.RegularExpressions.Regex.IsMatch(
                options.ContainerName,
                @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z"),
        "Nama container storage tidak valid.")
    .Validate(
        options =>
            Uri.TryCreate(
                options.PublicServiceUrl,
                UriKind.Absolute,
                out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps
                || (options.AllowHttp
                    && uri.Scheme == Uri.UriSchemeHttp))
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && string.IsNullOrEmpty(uri.UserInfo),
        "URL storage tidak valid.")
    .Validate(
        options => options.ReadUrlMinutes >= 5
            && options.ReadUrlMinutes <= 60,
        "Masa berlaku URL gambar harus 5–60 menit.")
    .Validate(
        options => options.MaxFileBytes > 0
            && options.MaxFileBytes <= 5 * 1024 * 1024,
        "Batas ukuran gambar maksimal 5 MiB.")
    .Validate(
        options => !options.AllowHttp
            || builder.Environment.IsDevelopment(),
        "HTTP storage hanya diizinkan untuk development.")
    .ValidateOnStart();

builder.Services.AddSingleton<
    IProductImageStorage,
    AzureBlobProductImageStorage>();

builder.Services.AddSingleton<
    IProductImageProcessor,
    ImageSharpProductImageProcessor>();

builder.Services.AddScoped<
    IProductImageService,
    ProductImageService>();

builder.Services.AddHostedService<StorageDeletionWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>(
    "HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
