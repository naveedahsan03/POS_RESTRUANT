using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class PurchasesController : Controller
{
    private readonly IPurchaseService _purchaseService;
    private readonly IProductService _productService;
    private readonly IBusinessDayService _businessDayService;

    public PurchasesController(
        IPurchaseService purchaseService,
        IProductService productService,
        IBusinessDayService businessDayService)
    {
        _purchaseService = purchaseService;
        _productService = productService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(DateTime? businessDate = null)
    {
        ViewBag.Suppliers = await _purchaseService.GetSuppliersAsync();
        ViewBag.Products = await _productService.GetProductsAsync(activeOnly: true);
        ViewBag.PurchaseReturns = await _purchaseService.GetPurchaseReturnsAsync(businessDate);
        ViewBag.SelectedBusinessDate = businessDate;

        var purchases = await _purchaseService.GetPurchasesAsync(businessDate);
        return View(purchases);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePurchase(CreatePurchaseRequestDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var purchase = await _purchaseService.CreatePurchaseAsync(request, userId);
            TempData["Success"] = $"Purchase Invoice {purchase.PurchaseInvoiceNumber} saved. Stock and Ledger automatically updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplier(Supplier supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            TempData["Error"] = "Supplier name is required.";
            return RedirectToAction(nameof(Index));
        }

        await _purchaseService.SaveSupplierAsync(supplier);
        TempData["Success"] = $"Supplier '{supplier.Name}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Return(int id)
    {
        var purchase = await _purchaseService.GetPurchaseWithDetailsAsync(id);
        if (purchase == null) return NotFound();
        return View(purchase);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReturn(CreatePurchaseReturnRequestDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var ret = await _purchaseService.CreatePurchaseReturnAsync(request, userId);
            TempData["Success"] = $"Purchase Return {ret.ReturnNumber} (Rs. {ret.TotalReturnAmount:N2}) processed. Stock decreased and Supplier payable updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Return), new { id = request.PurchaseId });
        }
    }
}
