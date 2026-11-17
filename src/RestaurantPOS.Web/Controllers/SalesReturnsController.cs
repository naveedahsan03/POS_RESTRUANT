using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin,Cashier")]
public class SalesReturnsController : Controller
{
    private readonly ISalesReturnService _salesReturnService;
    private readonly IPosSaleService _posSaleService;
    private readonly IBusinessDayService _businessDayService;

    public SalesReturnsController(
        ISalesReturnService salesReturnService,
        IPosSaleService posSaleService,
        IBusinessDayService businessDayService)
    {
        _salesReturnService = salesReturnService;
        _posSaleService = posSaleService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(string? invoiceNumber = null, DateTime? businessDate = null)
    {
        ViewBag.SearchInvoice = invoiceNumber;
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var sale = await _posSaleService.GetSaleByInvoiceNumberAsync(invoiceNumber);
            if (sale == null)
            {
                TempData["Error"] = $"No sale found with Invoice Number '{invoiceNumber}'.";
            }
            ViewBag.SearchedSale = sale;
        }

        var returns = await _salesReturnService.GetSalesReturnsAsync(businessDate);
        return View(returns);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSalesReturnRequestDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _salesReturnService.CreateSalesReturnAsync(request, userId);
            TempData["Success"] = $"Sales Return {result.ReturnNumber} processed (Refund: Rs. {result.TotalRefundAmount:N2}). Stock and Daily Reports updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
