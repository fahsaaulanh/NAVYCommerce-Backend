using System;
using System.Collections.Generic;
using System.Linq;
using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services
{
  public class ProductVariantFactory : IProductVariantFactory
  {
    private const int MaxOptions = 3;
    private const int MaxValuesPerOption = 20;
    private const int MaxVariants = 100;

    public List<ProductVariant> CreateVariants(Product product)
    {
      ArgumentNullException.ThrowIfNull(product);

      if (product.Id == Guid.Empty)
      {
        throw new ArgumentException(
            "Produk harus memiliki ID.",
            nameof(product));
      }

      var options = product.Options
          .OrderBy(option => option.SortOrder)
          .ThenBy(option => option.Id)
          .ToList();

      if (options.Count > MaxOptions)
      {
        throw new ArgumentException(
            "Produk maksimal memiliki 3 jenis opsi.",
            nameof(product));
      }

      var combinations = new List<List<ProductOptionValue>>
            {
                new()
            };

      foreach (var option in options)
      {
        var values = option.Values
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id)
            .ToList();

        ValidateOption(product, option, values);

        if (combinations.Count * values.Count > MaxVariants)
        {
          throw new ArgumentException(
              "Total kombinasi varian maksimal 100.",
              nameof(product));
        }

        var nextCombinations =
            new List<List<ProductOptionValue>>();

        foreach (var combination in combinations)
        {
          foreach (var value in values)
          {
            var next = new List<ProductOptionValue>(
                combination)
                        {
                            value
                        };

            nextCombinations.Add(next);
          }
        }

        combinations = nextCombinations;
      }

      var variants = new List<ProductVariant>();

      for (var index = 0; index < combinations.Count; index++)
      {
        var variant = new ProductVariant
        {
          Id = Guid.NewGuid(),
          ProductId = product.Id,
          Product = product,
          Sku = $"PRD-{product.Id:N}-{index + 1:D3}",
          PriceAdjustment = 0,
          StockQuantity = 0
        };

        foreach (var value in combinations[index])
        {
          variant.OptionValues.Add(
              new ProductVariantOptionValue
              {
                ProductId = product.Id,
                VariantId = variant.Id,
                OptionId = value.OptionId,
                OptionValueId = value.Id,
                Variant = variant,
                OptionValue = value
              });
        }

        variants.Add(variant);
      }

      return variants;
    }

    private static void ValidateOption(
        Product product,
        ProductOption option,
        List<ProductOptionValue> values)
    {
      if (option.Id == Guid.Empty ||
          option.ProductId != product.Id)
      {
        throw new ArgumentException(
            "Opsi harus memiliki ID dan berasal dari produk yang sama.");
      }

      if (values.Count < 1 ||
          values.Count > MaxValuesPerOption)
      {
        throw new ArgumentException(
            "Setiap opsi harus memiliki 1–20 pilihan.");
      }

      if (values.Any(value =>
          value.Id == Guid.Empty ||
          value.ProductId != product.Id ||
          value.OptionId != option.Id))
      {
        throw new ArgumentException(
            "Pilihan harus memiliki ID dan berasal dari opsi serta produk yang sama.");
      }

      if (values.Select(value => value.Id).Distinct().Count()
          != values.Count)
      {
        throw new ArgumentException(
            "ID pilihan dalam satu opsi tidak boleh duplikat.");
      }
    }
  }
}