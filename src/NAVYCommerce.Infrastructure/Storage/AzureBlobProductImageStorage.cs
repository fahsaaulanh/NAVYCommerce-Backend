using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using NAVYCommerce.Application.Abstractions.Storage;

namespace NAVYCommerce.Infrastructure.Storage
{
    public sealed class AzureBlobProductImageStorage
        : IProductImageStorage
    {
        private static readonly Regex StorageKeyPattern = new(
            @"\Aproducts/[0-9a-f]{32}/[0-9a-f]{32}\.(jpg|png|webp)\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly BlobContainerClient _container;
        private readonly ProductImageStorageOptions _options;
        private readonly Uri _publicServiceUri;

        public AzureBlobProductImageStorage(
            IOptions<ProductImageStorageOptions> options)
        {
            _options = options.Value;

            _publicServiceUri = new Uri(
                _options.PublicServiceUrl.TrimEnd('/') + "/",
                UriKind.Absolute);

            var clientOptions = new BlobClientOptions(
                BlobClientOptions.ServiceVersion.V2025_11_05);

            var serviceClient = new BlobServiceClient(
                _options.ConnectionString,
                clientOptions);

            _container = serviceClient.GetBlobContainerClient(
                _options.ContainerName);

            if (!_container.CanGenerateSasUri)
            {
                throw new InvalidOperationException(
                    "Cấu hình storage phải hỗ trợ tạo SAS bằng account key.");
            }
        }

        public async Task<string> SaveAsync(
            Guid productId,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException(
                    "ID produk tidak valid.",
                    nameof(productId));
            }

            ArgumentNullException.ThrowIfNull(content);

            if (!content.CanRead)
            {
                throw new ArgumentException(
                    "Stream gambar tidak dapat dibaca.",
                    nameof(content));
            }

            var extension = contentType switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/webp" => "webp",
                _ => throw new ArgumentException(
                    "Format gambar harus JPEG, PNG, atau WebP.",
                    nameof(contentType))
            };

            using var uploadContent = new MemoryStream();

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await content.ReadAsync(
                buffer.AsMemory(),
                cancellationToken)) > 0)
            {
                if (uploadContent.Length + bytesRead
                    > _options.MaxFileBytes)
                {
                    throw new InvalidDataException(
                        "Ukuran gambar melebihi batas yang diizinkan.");
                }

                await uploadContent.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            if (uploadContent.Length == 0)
            {
                throw new InvalidDataException(
                    "File gambar tidak boleh kosong.");
            }

            uploadContent.Position = 0;

            await _container.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.None,
                cancellationToken: cancellationToken);

            var properties = await _container.GetPropertiesAsync(
                cancellationToken: cancellationToken);

            if (properties.Value.PublicAccess != PublicAccessType.None)
            {
                throw new InvalidOperationException(
                    "Container gambar harus menggunakan akses private.");
            }

            var storageKey =
                $"products/{productId:N}/{Guid.NewGuid():N}.{extension}";

            var blob = _container.GetBlobClient(storageKey);

            await blob.UploadAsync(
                uploadContent,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType,
                        CacheControl = "private, max-age=300"
                    },
                    Conditions = new BlobRequestConditions
                    {
                        IfNoneMatch = ETag.All
                    }
                },
                cancellationToken);

            return storageKey;
        }

        public async Task DeleteAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            ValidateStorageKey(storageKey);

            var blob = _container.GetBlobClient(storageKey);

            await blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);
        }

        public Task<ProductImageReadUrl> CreateReadUrlAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidateStorageKey(storageKey);

            var blob = _container.GetBlobClient(storageKey);

            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(
                _options.ReadUrlMinutes);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = storageKey,
                Resource = "b",
                ExpiresOn = expiresAt,
                Protocol = _options.AllowHttp
                    ? SasProtocol.HttpsAndHttp
                    : SasProtocol.Https
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var signedUri = blob.GenerateSasUri(sasBuilder);

            var publicBlobUri = new Uri(
                _publicServiceUri,
                $"{_container.Name}/{storageKey}");

            var browserUrl =
                publicBlobUri.AbsoluteUri + signedUri.Query;

            return Task.FromResult(
                new ProductImageReadUrl(browserUrl, expiresAt));
        }

        private static void ValidateStorageKey(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey)
                || !StorageKeyPattern.IsMatch(storageKey))
            {
                throw new ArgumentException(
                    "Identitas file gambar tidak valid.",
                    nameof(storageKey));
            }
        }
    }
}