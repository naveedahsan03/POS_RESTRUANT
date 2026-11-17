using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.Interfaces;

namespace RestaurantPOS.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IReportService _reportService;
    private readonly IBusinessDayService _businessDayService;

    public HomeController(IReportService reportService, IBusinessDayService businessDayService)
    {
        _reportService = reportService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index()
    {
        var nowPkt = _businessDayService.GetNowPakistanTime();
        var todayBusinessDate = _businessDayService.CalculateBusinessDate(nowPkt);
        var dashboardReport = await _reportService.GetDailyBusinessReportAsync(todayBusinessDate, todayBusinessDate, "Today");

        ViewBag.CurrentPktTime = nowPkt;
        return View(dashboardReport);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
