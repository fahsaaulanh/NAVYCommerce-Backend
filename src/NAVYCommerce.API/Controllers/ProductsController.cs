using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;
using NAVYCommerce.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace NAVYCommerce.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ProductsController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly IVariantGeneratorService _variantGenerator;

    public ProductsController(ApplicationDbContext context, IVariantGeneratorService variantGenerator)
    {
      _context = context;
      _variantGenerator = variantGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
      var products = await _context.Products.ToListAsync();
      return Ok(products);
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
      if (product.Id == Guid.Empty)
      {
        product.Id = Guid.NewGuid();
      }
      product.CreatedAt = DateTime.UtcNow;

      if (product.VariantOptions != null && product.VariantOptions.Count > 0)
      {
        var generatedVariants = _variantGenerator.GenerateVariantsForProduct(product);

        foreach (var variant in generatedVariants)
        {
          product.Variants.Add(variant);
        }
      }

      _context.Products.Add(product);
      await _context.SaveChangesAsync();
      return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, product);
    }

    [HttpPut("{productId}/variants/{variantId}")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> UpdateVariant(Guid productId, Guid variantId, [FromBody] UpdateVariantRequest request)
    {
      var variant = await _context.ProductVariants
      .FirstOrDefaultAsync(v => v.ProductId == productId && v.Id == variantId);

      if (variant == null)
      {
        return NotFound(new { message = "Varian tidak ditemukan pda produk ini" });
      }

      variant.StockQuantity = request.StockQuantity;
      variant.PriceAdjustment = request.PriceAdjustment;

      await _context.SaveChangesAsync();
      return Ok(new { message = "Varian berhasil diperbarui", Variant = variant });
    }

  }

  public class UpdateVariantRequest
  {
    public decimal PriceAdjustment { get; set; }
    public int StockQuantity { get; set; }
  }

}