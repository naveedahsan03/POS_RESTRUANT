using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index(string? search = null, int? categoryId = null, bool lowStockOnly = false)
    {
        ViewBag.Categories = await _productService.GetCategoriesAsync(includeInactive: true);
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.LowStockOnly = lowStockOnly;

        var products = await _productService.GetProductsAsync(search, categoryId, lowStockOnly, activeOnly: false);
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _productService.GetCategoriesAsync();
        return View("Edit", new ProductDto { IsActive = true, Unit = "Piece", MinimumStock = 10 });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null) return NotFound();

        ViewBag.Categories = await _productService.GetCategoriesAsync(includeInactive: true);
        return View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ProductDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _productService.GetCategoriesAsync(includeInactive: true);
            return View("Edit", dto);
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (dto.Id == 0)
            {
                await _productService.CreateProductAsync(dto, userId);
                TempData["Success"] = $"Product '{dto.Name}' created and Opening Stock recorded in Stock Ledger.";
            }
            else
            {
                await _productService.UpdateProductAsync(dto, userId);
                TempData["Success"] = $"Product '{dto.Name}' updated successfully.";
            }
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Categories = await _productService.GetCategoriesAsync(includeInactive: true);
            return View("Edit", dto);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.SoftDeleteProductAsync(id);
        TempData["Success"] = "Product archived (soft-deleted) successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        await _productService.SaveCategoryAsync(category);
        TempData["Success"] = $"Category '{category.Name}' saved.";
        return RedirectToAction(nameof(Index));
    }
}
