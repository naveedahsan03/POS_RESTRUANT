using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly IBusinessDayService _businessDayService;

    public InventoryController(
        IInventoryService inventoryService,
        IProductService productService,
        IBusinessDayService businessDayService)
    {
        _inventoryService = inventoryService;
        _productService = productService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(int? productId = null, DateTime? startBusinessDate = null, DateTime? endBusinessDate = null)
    {
        ViewBag.Products = await _productService.GetProductsAsync();
        ViewBag.SelectedProductId = productId;
        ViewBag.StartBusinessDate = startBusinessDate;
        ViewBag.EndBusinessDate = endBusinessDate;

        ViewBag.StockSummary = await _inventoryService.GetStockSummaryAsync(startBusinessDate, endBusinessDate);
        var ledger = await _inventoryService.GetProductStockHistoryAsync(productId, startBusinessDate, endBusinessDate);
        return View(ledger);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(StockAdjustmentRequestDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole("Admin");
            var adj = await _inventoryService.AdjustStockAsync(request, userId, isAdmin);
            TempData["Success"] = $"Stock adjustment {adj.AdjustmentNumber} saved. New Stock: {adj.NewStock:N2}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { productId = request.ProductId });
    }
}
