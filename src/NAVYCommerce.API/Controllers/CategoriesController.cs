using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NAVYCommerce.Domain.Entities;
using NAVYCommerce.Infrastructure.Data;

namespace NAVYCommerce.API.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class CategoriesController : ControllerBase
  {
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
      _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
      var categories = await _context.Categories.ToListAsync();
      return Ok(categories);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] Category request)
    {
      var existingCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Slug == request.Slug);

      if (existingCategory != null)
      {
        return BadRequest(new { message = "Category slug already exists." });
      }

      var newCategory = new Category
      {
        Name = request.Name,
        Slug = request.Slug,
        ParentId = request.ParentId
      };

      _context.Categories.Add(newCategory);
      await _context.SaveChangesAsync();

      return CreatedAtAction(nameof(GetCategories), new { id = newCategory.Id }, new
      {
        message = "Category created successfully",
        data = newCategory
      });
    }
  }
}