using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVYCommerce.API.Contracts.Products;
using NAVYCommerce.API.Services;

namespace NAVYCommerce.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ProductsController : ControllerBase
  {
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
      _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
      var result = await _productService.GetPublicProductsAsync(
          page,
          pageSize,
          cancellationToken);

      return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var result = await _productService.CreateAsync(
          userId,
          request,
          cancellationToken);

      switch (result.Status)
      {
        case CreateProductStatus.Created:
          return CreatedAtAction(
              nameof(GetOwnedProduct),
              new { productId = result.Product!.Id },
              new
              {
                code = "PRODUCT_DRAFT_CREATED",
                message = "Draft produk berhasil dibuat.",
                data = result.Product
              });

        case CreateProductStatus.StoreNotFound:
          return NotFound(new
          {
            code = "STORE_NOT_FOUND",
            message = "Toko tidak ditemukan atau tidak dapat diakses."
          });

        case CreateProductStatus.CategoryNotFound:
          return BadRequest(new
          {
            code = "PRODUCT_CATEGORY_INVALID",
            message = "Kategori yang dipilih tidak tersedia."
          });

        case CreateProductStatus.SlugAlreadyUsed:
          return Conflict(new
          {
            code = "PRODUCT_SLUG_ALREADY_USED",
            message = "Slug produk sudah digunakan."
          });

        default:
          throw new InvalidOperationException(
              "Status pembuatan produk tidak dikenali.");
      }
    }

    [HttpGet("mine/{productId:guid}")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> GetOwnedProduct(
        Guid productId,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var product = await _productService.GetOwnedProductAsync(
          userId,
          productId,
          cancellationToken);

      if (product == null)
      {
        return NotFound(new
        {
          code = "PRODUCT_NOT_FOUND",
          message = "Produk tidak ditemukan."
        });
      }

      Response.Headers["Cache-Control"] = "no-store";

      return Ok(new { data = product });
    }

    [HttpPut("{productId:guid}/variants/{variantId:guid}")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> UpdateVariant(
        Guid productId,
        Guid variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var result = await _productService.UpdateVariantAsync(
          userId,
          productId,
          variantId,
          request,
          cancellationToken);

      switch (result.Status)
      {
        case UpdateProductVariantStatus.Updated:
          return Ok(new
          {
            code = "PRODUCT_VARIANT_UPDATED",
            message = "Varian berhasil diperbarui.",
            data = result.Variant
          });

        case UpdateProductVariantStatus.NotFound:
          return NotFound(new
          {
            code = "PRODUCT_VARIANT_NOT_FOUND",
            message = "Varian tidak ditemukan pada produk yang dapat diakses."
          });

        case UpdateProductVariantStatus.InvalidPrice:
          return BadRequest(new
          {
            code = "PRODUCT_VARIANT_PRICE_INVALID",
            message = "Harga akhir varian harus antara 0 dan 999.999.999.999,99."
          });

        default:
          throw new InvalidOperationException(
              "Status pembaruan varian tidak dikenali.");
      }
    }

    [HttpGet("mine/store/{storeId:guid}")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnedByStore(
    Guid storeId,
    CancellationToken cancellationToken,
    [FromQuery, Range(1, 1_000_000)] int page = 1,
    [FromQuery, Range(1, 100)] int pageSize = 12)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return Unauthorized(new
        {
          code = "AUTH_INVALID_IDENTITY",
          message = "Identitas user tidak valid."
        });
      }

      var result = await _productService.GetOwnedByStoreAsync(
          userId,
          storeId,
          page,
          pageSize,
          cancellationToken);

      if (result == null)
      {
        return NotFound(new
        {
          code = "STORE_NOT_FOUND",
          message = "Toko tidak ditemukan."
        });
      }

      return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
      var value =
          User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? User.FindFirstValue("sub");

      return Guid.TryParse(value, out userId);
    }

    private IActionResult InvalidIdentity()
    {
      return Unauthorized(new
      {
        code = "AUTH_INVALID_IDENTITY",
        message = "Identitas user tidak valid."
      });
    }
  }
}