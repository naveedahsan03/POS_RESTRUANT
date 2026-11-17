using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin,Cashier")]
public class PosController : Controller
{
    private readonly IProductService _productService;
    private readonly IPosSaleService _posSaleService;
    private readonly IBusinessDayService _businessDayService;

    public PosController(
        IProductService productService,
        IPosSaleService posSaleService,
        IBusinessDayService businessDayService)
    {
        _productService = productService;
        _posSaleService = posSaleService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(int? resumeSaleId = null)
    {
        var nowPkt = _businessDayService.GetNowPakistanTime();
        var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);
        var (windowStart, windowEnd) = _businessDayService.GetBusinessDayWindow(businessDate);

        ViewBag.CurrentPktTime = nowPkt;
        ViewBag.CurrentBusinessDate = businessDate;
        ViewBag.WindowStart = windowStart;
        ViewBag.WindowEnd = windowEnd;

        ViewBag.Categories = await _productService.GetCategoriesAsync();
        ViewBag.Products = await _productService.GetProductsAsync(activeOnly: true);
        ViewBag.Tables = await _posSaleService.GetTablesAsync();
        ViewBag.HeldOrders = await _posSaleService.GetHeldOrdersAsync();

        if (resumeSaleId.HasValue)
        {
            ViewBag.ResumedOrder = await _posSaleService.GetSaleReceiptAsync(resumeSaleId.Value);
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SubmitOrder([FromBody] CreateSaleRequestDto request)
    {
        if (request == null || !request.Items.Any())
        {
            return BadRequest(new { success = false, message = "Cart is empty. Please add items before saving." });
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole("Admin");
            var receipt = await _posSaleService.CreateOrCompleteSaleAsync(request, userId, isAdmin);

            return Ok(new
            {
                success = true,
                saleId = receipt.SaleId,
                invoiceNumber = receipt.InvoiceNumber,
                businessDate = receipt.BusinessDate.ToString("dd MMM yyyy"),
                totalAmount = receipt.TotalAmount,
                changeAmount = receipt.ChangeAmount,
                isHeld = receipt.Status == SaleStatus.Held,
                receiptUrl = Url.Action(nameof(Receipt), new { id = receipt.SaleId })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var receipt = await _posSaleService.GetSaleReceiptAsync(id);
        if (receipt == null) return NotFound();
        return View(receipt);
    }

    [HttpGet]
    public async Task<IActionResult> SalesHistory(DateTime? businessDate = null)
    {
        var targetDate = businessDate ?? _businessDayService.GetCurrentBusinessDate();
        ViewBag.SelectedBusinessDate = targetDate;
        var sales = await _posSaleService.GetRecentSalesAsync(targetDate, 200);
        return View(sales);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTableStatus(int tableId, TableStatus status)
    {
        await _posSaleService.UpdateTableStatusAsync(tableId, status);
        TempData["Success"] = "Table status updated.";
        return RedirectToAction(nameof(Index));
    }
}
