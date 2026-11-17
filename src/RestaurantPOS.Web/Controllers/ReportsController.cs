using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.Interfaces;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly IBusinessDayService _businessDayService;

    public ReportsController(IReportService reportService, IBusinessDayService businessDayService)
    {
        _reportService = reportService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> DailyReport(string preset = "Today", DateTime? startDate = null, DateTime? endDate = null)
    {
        var currentBusinessDate = _businessDayService.GetCurrentBusinessDate();
        DateTime start;
        DateTime end;

        switch (preset)
        {
            case "Yesterday":
                start = currentBusinessDate.AddDays(-1);
                end = start;
                break;
            case "Custom":
                start = (startDate ?? currentBusinessDate).Date;
                end = (endDate ?? start).Date;
                break;
            default:
                preset = "Today";
                start = currentBusinessDate;
                end = currentBusinessDate;
                break;
        }

        var report = await _reportService.GetDailyBusinessReportAsync(start, end, preset);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(DateTime startDate, DateTime endDate, string preset = "Custom")
    {
        var report = await _reportService.GetDailyBusinessReportAsync(startDate, endDate, preset);
        var bytes = await _reportService.ExportDailyReportToExcelAsync(report);
        var fileName = $"DailyBusinessReport_{report.StartBusinessDate:yyyyMMdd}_{report.EndBusinessDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
