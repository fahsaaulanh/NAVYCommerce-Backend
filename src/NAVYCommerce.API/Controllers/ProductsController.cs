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
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductCatalogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
      Response.Headers["Cache-Control"] = "no-store";
      var result = await _productService.GetPublicProductsAsync(request, cancellationToken);
      return Ok(result);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogCategories(CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";
      return Ok(await _productService.GetCatalogCategoriesAsync(cancellationToken));
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

    [HttpPut("{productId:guid}/publication")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetPublication(
    Guid productId,
    [FromBody] SetProductPublicationRequest request,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var result = await _productService.SetPublicationAsync(
          userId,
          productId,
          request.IsActive!.Value,
          cancellationToken);

      switch (result.Status)
      {
        case SetProductPublicationStatus.Updated:
          return Ok(new
          {
            code = result.Product!.IsActive
                  ? "PRODUCT_PUBLISHED"
                  : "PRODUCT_UNPUBLISHED",

            message = result.Product.IsActive
                  ? "Produk aktif dan tersedia di katalog."
                  : "Produk nonaktif dan tidak tampil di katalog.",

            data = result.Product
          });

        case SetProductPublicationStatus.NotFound:
          return NotFound(new
          {
            code = "PRODUCT_NOT_FOUND",
            message = "Produk tidak ditemukan."
          });

        case SetProductPublicationStatus.NotReady:
          return Conflict(new
          {
            code = "PRODUCT_NOT_READY",
            message = "Lengkapi produk sebelum publikasi.",
            issues = result.Issues
          });

        default:
          throw new InvalidOperationException(
              "Status publikasi produk tidak dikenali.");
      }
    }

    [HttpPut("{productId:guid}/options/{optionId:guid}/name")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> RenameOption(
      Guid productId,
      Guid optionId,
      [FromBody] RenameProductOptionRequest request,
      CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var result = await _productService.RenameOptionAsync(
          userId,
          productId,
          optionId,
          request.Name,
          cancellationToken);

      return result.Status switch
      {
        RenameProductLabelStatus.Updated => Ok(new
        {
          code = "PRODUCT_OPTION_RENAMED",
          message = "Nama opsi berhasil diperbarui.",
          data = new
          {
            id = optionId,
            name = result.Label
          }
        }),

        RenameProductLabelStatus.NotFound => NotFound(new
        {
          code = "PRODUCT_OPTION_NOT_FOUND",
          message = "Opsi tidak ditemukan pada produk yang dapat diakses."
        }),

        RenameProductLabelStatus.Duplicate => Conflict(new
        {
          code = "PRODUCT_OPTION_NAME_ALREADY_USED",
          message = "Nama opsi sudah digunakan pada produk ini."
        }),

        RenameProductLabelStatus.InvalidLabel => BadRequest(new
        {
          code = "PRODUCT_OPTION_NAME_INVALID",
          message = "Nama opsi wajib diisi dan maksimal 50 karakter."
        }),

        _ => throw new InvalidOperationException(
            "Status perubahan nama opsi tidak dikenali.")
      };
    }

    [HttpPut("{productId:guid}/option-values/{optionValueId:guid}/value")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> RenameOptionValue(
    Guid productId,
    Guid optionValueId,
    [FromBody] RenameProductOptionValueRequest request,
    CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      if (!TryGetUserId(out var userId))
      {
        return InvalidIdentity();
      }

      var result = await _productService.RenameOptionValueAsync(
          userId,
          productId,
          optionValueId,
          request.Value,
          cancellationToken);

      return result.Status switch
      {
        RenameProductLabelStatus.Updated => Ok(new
        {
          code = "PRODUCT_OPTION_VALUE_RENAMED",
          message = "Nilai pilihan berhasil diperbarui.",
          data = new
          {
            id = optionValueId,
            value = result.Label
          }
        }),

        RenameProductLabelStatus.NotFound => NotFound(new
        {
          code = "PRODUCT_OPTION_VALUE_NOT_FOUND",
          message = "Nilai pilihan tidak ditemukan pada produk yang dapat diakses."
        }),

        RenameProductLabelStatus.Duplicate => Conflict(new
        {
          code = "PRODUCT_OPTION_VALUE_ALREADY_USED",
          message = "Nilai pilihan sudah digunakan dalam opsi ini."
        }),

        RenameProductLabelStatus.InvalidLabel => BadRequest(new
        {
          code = "PRODUCT_OPTION_VALUE_INVALID",
          message = "Nilai pilihan wajib diisi dan maksimal 50 karakter."
        }),

        _ => throw new InvalidOperationException(
            "Status perubahan nilai pilihan tidak dikenali.")
      };
    }

    [HttpGet("{productId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProduct(
      Guid productId,
      CancellationToken cancellationToken)
    {
      Response.Headers["Cache-Control"] = "no-store";

      var result = await _productService.GetPublicProductAsync(
          productId,
          cancellationToken);

      if (result == null)
      {
        return NotFound(new
        {
          code = "PRODUCT_NOT_FOUND",
          message = "Produk tidak ditemukan atau belum tersedia."
        });
      }

      return Ok(new { data = result });
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