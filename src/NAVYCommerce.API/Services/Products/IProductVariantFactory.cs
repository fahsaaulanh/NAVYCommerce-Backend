using System.Collections.Generic;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services
{
  public interface IProductVariantFactory
  {
    List<ProductVariant> CreateVariants(Product product);
  }
}