using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NAVYCommerce.Application.Abstractions.Storage
{
  public interface IProductImageStorage
  {
    Task<string> SaveAsync(
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<ProductImageReadUrl> CreateReadUrlAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
  }
}