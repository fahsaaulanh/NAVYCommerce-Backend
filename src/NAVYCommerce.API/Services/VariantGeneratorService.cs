using NAVYCommerce.Domain.Entities;

namespace NAVYCommerce.API.Services
{
  public interface IVariantGeneratorService
  {
    List<ProductVariant> GenerateVariantsForProduct(Product product);
  }

  public class VariantGeneratorService : IVariantGeneratorService
  {
    public List<ProductVariant> GenerateVariantsForProduct(Product product)
    {
      var variants = new List<ProductVariant>();
      var combinations = new List<Dictionary<string, string>>();
      var keys = product.VariantOptions.Keys.ToList();

      GenerateCombinations(product.VariantOptions, keys, 0, new Dictionary<string, string>(), combinations);

      int skuCounter = 1;
      foreach (var combo in combinations)
      {
        var skuSuffix = string.Join("-", combo.Values).ToUpper().Replace(" ", " ");
        var generatedSku = $"PRD-{product.Id.ToString().Substring(0, 4).ToUpper()}-{skuSuffix}-{skuCounter}";

        var variant = new ProductVariant
        {
          Id = Guid.NewGuid(),
          ProductId = product.Id,
          Sku = generatedSku,
          VariantAttributes = combo,
          PriceAdjustment = 0,
          StockQuantity = 0
        };

        variants.Add(variant);
        skuCounter++;
      }
      return variants;
    }

    private void GenerateCombinations(
      Dictionary<string, List<string>> options,
      List<string> keys,
      int keyIndex,
      Dictionary<string, string> currentCombination,
      List<Dictionary<string, string>> result
    )
    {
      if (keyIndex == keys.Count)
      {
        result.Add(new Dictionary<string, string>(currentCombination));
        return;
      }

      string currentKey = keys[keyIndex];

      foreach (var value in options[currentKey])
      {
        currentCombination[currentKey] = value;
        GenerateCombinations(options, keys, keyIndex + 1, currentCombination, result);
      }
    }
  }
}