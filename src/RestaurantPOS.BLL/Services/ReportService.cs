using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public ReportService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<DailyBusinessReportDto> GetDailyBusinessReportAsync(
        DateTime startBusinessDate,
        DateTime endBusinessDate,
        string preset = "Custom")
    {
        var startDate = startBusinessDate.Date;
        var endDate = endBusinessDate.Date;
        if (endDate < startDate) endDate = startDate;

        var (windowStart, windowEnd) = _businessDayService.GetBusinessDateRangeWindow(startDate, endDate);

        // Fetch completed/partially-returned/returned sales belonging to the BusinessDate range
        var sales = await _db.Sales
            .Include(s => s.SaleDetails)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p!.Category)
            .Include(s => s.SalePayments)
            .Where(s => s.BusinessDate >= startDate && s.BusinessDate <= endDate &&
                        (s.Status == SaleStatus.Completed ||
                         s.Status == SaleStatus.PartiallyReturned ||
                         s.Status == SaleStatus.Returned))
            .ToListAsync();

        var heldOrdersCount = await _db.Sales
            .CountAsync(s => s.BusinessDate >= startDate && s.BusinessDate <= endDate && s.Status == SaleStatus.Held);

        var salesReturns = await _db.SalesReturns
            .Include(r => r.SalesReturnDetails)
            .Where(r => r.BusinessDate >= startDate && r.BusinessDate <= endDate)
            .ToListAsync();

        var expenses = await _db.Expenses
            .Where(e => e.BusinessDate >= startDate && e.BusinessDate <= endDate)
            .ToListAsync();

        var purchases = await _db.Purchases
            .Where(p => p.BusinessDate >= startDate && p.BusinessDate <= endDate)
            .ToListAsync();

        var purchaseReturns = await _db.PurchaseReturns
            .Where(pr => pr.BusinessDate >= startDate && pr.BusinessDate <= endDate)
            .ToListAsync();

        // 1. Gross Sales (Before order-level and line-level discounts)
        var allSaleDetails = sales.SelectMany(s => s.SaleDetails).ToList();
        var lineGrossSum = allSaleDetails.Sum(d => Math.Round(d.Quantity * d.UnitPrice, 2));
        var lineDiscountSum = allSaleDetails.Sum(d => d.DiscountAmount);
        var orderDiscountSum = sales.Sum(s => s.DiscountAmount);
        var totalDiscounts = lineDiscountSum + orderDiscountSum;
        var totalTax = sales.Sum(s => s.TaxAmount);

        var totalGrossSales = lineGrossSum;
        var totalSalesReturns = salesReturns.Sum(r => r.TotalRefundAmount);

        // Net Sales = Gross Sales - Discounts - Sales Returns
        var netSales = totalGrossSales - totalDiscounts - totalSalesReturns;

        // Payments Breakdown (Reconciled from SalePayments)
        var allPayments = sales.SelectMany(s => s.SalePayments).ToList();
        var cashSalesPaid = allPayments.Where(p => p.PaymentMethod == PaymentMethodType.Cash).Sum(p => p.Amount);
        var cardSalesPaid = allPayments.Where(p => p.PaymentMethod == PaymentMethodType.Card).Sum(p => p.Amount);
        var onlineSalesPaid = allPayments.Where(p => p.PaymentMethod == PaymentMethodType.Online).Sum(p => p.Amount);

        // Deduct cash refunds from net cash received
        var cashRefunds = salesReturns.Where(r => r.RefundMethod == PaymentMethodType.Cash).Sum(r => r.TotalRefundAmount);
        var netCashReceived = Math.Max(0m, cashSalesPaid - cashRefunds);

        var totalExpenses = expenses.Sum(e => e.Amount);
        var totalPurchases = purchases.Sum(p => p.TotalAmount);
        var totalPurchaseReturns = purchaseReturns.Sum(pr => pr.TotalReturnAmount);

        // COGS = Cost of Sold Items - Cost of Returned Sold Items
        var grossCogs = allSaleDetails.Sum(d => Math.Round(d.Quantity * d.UnitCostSnapshot, 2));
        var returnedCogs = salesReturns
            .SelectMany(r => r.SalesReturnDetails)
            .Sum(rd => Math.Round(rd.ReturnQuantity * rd.UnitCostSnapshot, 2));
        var estimatedCogs = Math.Max(0m, grossCogs - returnedCogs);

        var grossProfit = netSales - estimatedCogs;
        var estimatedNetProfit = grossProfit - totalExpenses;

        var totalOrders = sales.Count;
        var avgOrderValue = totalOrders > 0 ? Math.Round(netSales / totalOrders, 2) : 0m;

        // Product-wise Sales Breakdown
        var productWise = allSaleDetails
            .GroupBy(d => new
            {
                d.ProductId,
                d.ProductName,
                CategoryName = d.Product?.Category?.Name ?? "Uncategorized"
            })
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                var gross = g.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
                var disc = g.Sum(x => x.DiscountAmount);
                var net = g.Sum(x => x.LineTotal);
                var cost = g.Sum(x => Math.Round(x.Quantity * x.UnitCostSnapshot, 2));
                return new ProductSalesBreakdownDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = qty,
                    GrossAmount = gross,
                    DiscountAmount = disc,
                    NetAmount = net,
                    EstimatedCost = cost,
                    EstimatedProfit = net - cost
                };
            })
            .OrderByDescending(x => x.NetAmount)
            .ToList();

        // Category-wise Sales Breakdown
        var totalProductNet = productWise.Sum(x => x.NetAmount);
        var categoryWise = productWise
            .GroupBy(x => x.CategoryName)
            .Select(g =>
            {
                var catSales = g.Sum(x => x.NetAmount);
                return new CategorySalesBreakdownDto
                {
                    CategoryName = g.Key,
                    QuantitySold = g.Sum(x => x.QuantitySold),
                    TotalSalesAmount = catSales,
                    PercentageOfSales = totalProductNet > 0 ? Math.Round((catSales / totalProductNet) * 100m, 1) : 0m
                };
            })
            .OrderByDescending(x => x.TotalSalesAmount)
            .ToList();

        // Payment-wise Breakdown
        var paymentWise = allPayments
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new PaymentSalesBreakdownDto
            {
                PaymentMethod = g.Key.ToString(),
                TransactionCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        // Hourly Sales Breakdown (Ordered for 6:00 PM -> 4:00 AM shift)
        int[] shiftHoursOrder = [18, 19, 20, 21, 22, 23, 0, 1, 2, 3];
        var salesByHour = sales.GroupBy(s => s.SaleDateTime.Hour)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(s => s.TotalAmount)));

        var hourlyList = new List<HourlySalesBreakdownDto>();
        foreach (var h in shiftHoursOrder)
        {
            salesByHour.TryGetValue(h, out var stat);
            hourlyList.Add(new HourlySalesBreakdownDto
            {
                Hour24 = h,
                HourLabel = FormatHourWindow(h),
                OrderCount = stat.Count,
                TotalSales = stat.Amount
            });
        }

        // Also include any off-shift hours that had transactions
        foreach (var kvp in salesByHour.Where(k => !shiftHoursOrder.Contains(k.Key)).OrderBy(k => k.Key))
        {
            hourlyList.Add(new HourlySalesBreakdownDto
            {
                Hour24 = kvp.Key,
                HourLabel = FormatHourWindow(kvp.Key) + " (Off-Shift)",
                OrderCount = kvp.Value.Count,
                TotalSales = kvp.Value.Amount
            });
        }

        // Low stock alerts
        var lowStockProducts = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "",
                PurchasePrice = p.PurchasePrice,
                SalePrice = p.SalePrice,
                CurrentStock = p.CurrentStock,
                MinimumStock = p.MinimumStock,
                Unit = p.Unit
            })
            .ToListAsync();

        return new DailyBusinessReportDto
        {
            StartBusinessDate = startDate,
            EndBusinessDate = endDate,
            ExactWindowStart = windowStart,
            ExactWindowEnd = windowEnd,
            FilterPreset = preset,
            TotalGrossSales = totalGrossSales,
            TotalDiscounts = totalDiscounts,
            TotalTax = totalTax,
            SalesReturns = totalSalesReturns,
            NetSales = netSales,
            CashReceived = netCashReceived,
            CardPayments = cardSalesPaid,
            OnlinePayments = onlineSalesPaid,
            TotalExpenses = totalExpenses,
            PurchaseAmount = totalPurchases,
            PurchaseReturnsAmount = totalPurchaseReturns,
            EstimatedCostOfGoodsSold = estimatedCogs,
            GrossProfit = grossProfit,
            EstimatedNetProfit = estimatedNetProfit,
            TotalOrders = totalOrders,
            HeldOrdersCount = heldOrdersCount,
            AverageOrderValue = avgOrderValue,
            ProductWiseSales = productWise,
            CategoryWiseSales = categoryWise,
            PaymentWiseSales = paymentWise,
            HourlySales = hourlyList,
            LowStockProducts = lowStockProducts
        };
    }

    private static string FormatHourWindow(int hour24)
    {
        var dt = DateTime.Today.AddHours(hour24);
        var endDt = dt.AddMinutes(59);
        var suffix = hour24 < 4 ? " (After Midnight)" : "";
        return $"{dt:hh:mm tt} - {endDt:hh:mm tt}{suffix}";
    }

    public Task<byte[]> ExportDailyReportToExcelAsync(DailyBusinessReportDto report)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Executive Financial Summary
        var ws = workbook.Worksheets.Add("Daily Business Summary");
        ws.Cell(1, 1).Value = "RESTAURANT POS - DAILY BUSINESS REPORT";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = "Selected Business Date(s):";
        ws.Cell(2, 2).Value = report.StartBusinessDate == report.EndBusinessDate
            ? report.StartBusinessDate.ToString("dd MMM yyyy")
            : $"{report.StartBusinessDate:dd MMM yyyy} to {report.EndBusinessDate:dd MMM yyyy}";

        ws.Cell(3, 1).Value = "Business Window (PKT):";
        ws.Cell(3, 2).Value = $"{report.ExactWindowStart:dd MMM yyyy hh:mm tt} to {report.ExactWindowEnd:dd MMM yyyy hh:mm tt}";

        int row = 5;
        ws.Cell(row, 1).Value = "Metric";
        ws.Cell(row, 2).Value = "Amount (PKR)";
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;

        (string Label, decimal Value)[] metrics =
        [
            ("1. Total Gross Sales", report.TotalGrossSales),
            ("2. Total Discounts", report.TotalDiscounts),
            ("3. Sales Returns", report.SalesReturns),
            ("4. Net Sales", report.NetSales),
            ("5. Cash Received (Net)", report.CashReceived),
            ("6. Card Payments", report.CardPayments),
            ("7. Online Payments", report.OnlinePayments),
            ("8. Total Expenses", report.TotalExpenses),
            ("9. Purchase Amount", report.PurchaseAmount),
            ("10. Estimated Cost of Goods Sold (COGS)", report.EstimatedCostOfGoodsSold),
            ("11. Gross Profit", report.GrossProfit),
            ("12. Estimated Net Profit", report.EstimatedNetProfit),
            ("Total Orders Count", report.TotalOrders),
            ("Average Order Value", report.AverageOrderValue)
        ];

        foreach (var m in metrics)
        {
            row++;
            ws.Cell(row, 1).Value = m.Label;
            ws.Cell(row, 2).Value = m.Value;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        }
        ws.Columns().AdjustToContents();

        // Sheet 2: Product-wise Sales
        var wsProd = workbook.Worksheets.Add("Product Sales");
        wsProd.Cell(1, 1).Value = "Product";
        wsProd.Cell(1, 2).Value = "Category";
        wsProd.Cell(1, 3).Value = "Qty Sold";
        wsProd.Cell(1, 4).Value = "Gross (PKR)";
        wsProd.Cell(1, 5).Value = "Discount (PKR)";
        wsProd.Cell(1, 6).Value = "Net Sales (PKR)";
        wsProd.Cell(1, 7).Value = "Est. Cost (PKR)";
        wsProd.Cell(1, 8).Value = "Est. Profit (PKR)";
        wsProd.Range(1, 1, 1, 8).Style.Font.Bold = true;

        int pRow = 2;
        foreach (var item in report.ProductWiseSales)
        {
            wsProd.Cell(pRow, 1).Value = item.ProductName;
            wsProd.Cell(pRow, 2).Value = item.CategoryName;
            wsProd.Cell(pRow, 3).Value = item.QuantitySold;
            wsProd.Cell(pRow, 4).Value = item.GrossAmount;
            wsProd.Cell(pRow, 5).Value = item.DiscountAmount;
            wsProd.Cell(pRow, 6).Value = item.NetAmount;
            wsProd.Cell(pRow, 7).Value = item.EstimatedCost;
            wsProd.Cell(pRow, 8).Value = item.EstimatedProfit;
            pRow++;
        }
        wsProd.Columns().AdjustToContents();

        // Sheet 3: Hourly Sales
        var wsHour = workbook.Worksheets.Add("Hourly Sales (6PM-4AM)");
        wsHour.Cell(1, 1).Value = "Shift Hour Slot";
        wsHour.Cell(1, 2).Value = "Orders";
        wsHour.Cell(1, 3).Value = "Total Sales (PKR)";
        wsHour.Range(1, 1, 1, 3).Style.Font.Bold = true;

        int hRow = 2;
        foreach (var h in report.HourlySales)
        {
            wsHour.Cell(hRow, 1).Value = h.HourLabel;
            wsHour.Cell(hRow, 2).Value = h.OrderCount;
            wsHour.Cell(hRow, 3).Value = h.TotalSales;
            hRow++;
        }
        wsHour.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }
}
