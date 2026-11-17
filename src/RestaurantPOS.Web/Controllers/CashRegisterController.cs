using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin,Cashier")]
public class CashRegisterController : Controller
{
    private readonly ICashRegisterService _cashRegisterService;
    private readonly IBusinessDayService _businessDayService;

    public CashRegisterController(ICashRegisterService cashRegisterService, IBusinessDayService businessDayService)
    {
        _cashRegisterService = cashRegisterService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(DateTime? businessDate = null)
    {
        var currentBDate = businessDate ?? _businessDayService.GetCurrentBusinessDate();
        var (windowStart, windowEnd) = _businessDayService.GetBusinessDayWindow(currentBDate);

        ViewBag.CurrentBusinessDate = currentBDate;
        ViewBag.WindowStart = windowStart;
        ViewBag.WindowEnd = windowEnd;
        ViewBag.History = await _cashRegisterService.GetRegisterHistoryAsync(20);

        var activeRegister = await _cashRegisterService.GetActiveRegisterAsync(currentBDate);
        return View(activeRegister);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(OpenCashRegisterDto dto)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var reg = await _cashRegisterService.OpenRegisterAsync(dto, userId);
            TempData["Success"] = $"Cash Register opened for Business Date {reg.BusinessDate:dd MMM yyyy} with Opening Cash Rs. {reg.OpeningCash:N2}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseCashRegisterDto dto)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var reg = await _cashRegisterService.CloseRegisterAsync(dto, userId);
            TempData["Success"] = $"Business Day {reg.BusinessDate:dd MMM yyyy} Cash Register closed. Expected: Rs. {reg.ExpectedClosingCash:N2}, Actual: Rs. {reg.ActualClosingCash:N2}, Difference: Rs. {reg.CashDifference:N2}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
