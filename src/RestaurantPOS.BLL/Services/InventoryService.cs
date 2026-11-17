using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public InventoryService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<StockAdjustment> AdjustStockAsync(StockAdjustmentRequestDto request, string userId, bool isAdmin)
    {
        if (request.QuantityChange == 0)
        {
            throw new InvalidOperationException("Adjustment quantity cannot be zero.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var nowPkt = _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId)
                ?? throw new InvalidOperationException("Product not found.");

            // Wastage is always a stock deduction
            var signedDelta = request.MovementType == StockMovementType.Wastage
                ? -Math.Abs(request.QuantityChange)
                : request.QuantityChange;

            var opening = product.CurrentStock;
            var closing = opening + signedDelta;

            if (closing < 0 && !product.AllowNegativeStock && !isAdmin)
            {
                throw new InvalidOperationException($"Adjustment would result in negative stock ({closing:N2}) for '{product.Name}'.");
            }

            var countToday = await _db.StockAdjustments.CountAsync(a => a.BusinessDate == businessDate);
            var adjNo = $"ADJ-{businessDate:yyyyMMdd}-{(countToday + 1):D4}";

            var adjustment = new StockAdjustment
            {
                AdjustmentNumber = adjNo,
                ProductId = product.Id,
                BusinessDate = businessDate,
                AdjustmentDateTime = nowPkt,
                MovementType = request.MovementType == StockMovementType.Wastage
                    ? StockMovementType.Wastage
                    : StockMovementType.StockAdjustment,
                PreviousStock = opening,
                AdjustedQuantity = signedDelta,
                NewStock = closing,
                Reason = request.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAt = nowPkt
            };

            _db.StockAdjustments.Add(adjustment);
            await _db.SaveChangesAsync();

            product.CurrentStock = closing;
            product.UpdatedAt = nowPkt;

            _db.StockLedger.Add(new StockLedger
            {
                ProductId = product.Id,
                BusinessDate = businessDate,
                TransactionDateTime = nowPkt,
                MovementType = adjustment.MovementType,
                ReferenceNumber = adjustment.AdjustmentNumber,
                ReferenceId = adjustment.Id,
                OpeningStock = opening,
                QuantityIn = signedDelta > 0 ? signedDelta : 0m,
                QuantityOut = signedDelta < 0 ? Math.Abs(signedDelta) : 0m,
                ClosingStock = closing,
                UnitCost = product.PurchasePrice,
                Remarks = $"{adjustment.MovementType}: {adjustment.Reason}",
                CreatedByUserId = userId,
                CreatedAt = nowPkt
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return adjustment;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<ProductStockSummaryDto>> GetStockSummaryAsync(DateTime? startBusinessDate = null, DateTime? endBusinessDate = null)
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .OrderBy(p => p.Category!.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var ledgerQuery = _db.StockLedger.AsQueryable();
        if (startBusinessDate.HasValue)
        {
            var s = startBusinessDate.Value.Date;
            ledgerQuery = ledgerQuery.Where(l => l.BusinessDate >= s);
        }
        if (endBusinessDate.HasValue)
        {
            var e = endBusinessDate.Value.Date;
            ledgerQuery = ledgerQuery.Where(l => l.BusinessDate <= e);
        }

        var ledgerEntries = await ledgerQuery
            .OrderBy(l => l.TransactionDateTime)
            .ThenBy(l => l.Id)
            .ToListAsync();

        var grouped = ledgerEntries.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ProductStockSummaryDto>();
        foreach (var p in products)
        {
            grouped.TryGetValue(p.Id, out var entries);
            decimal opening = 0m;
            decimal stockIn = 0m;
            decimal stockOut = 0m;
            decimal closing = p.CurrentStock;

            if (entries != null && entries.Any())
            {
                var first = entries.First();
                opening = first.MovementType == StockMovementType.OpeningStock
                    ? first.QuantityIn
                    : first.OpeningStock;

                stockIn = entries
                    .Where(x => x.MovementType != StockMovementType.OpeningStock)
                    .Sum(x => x.QuantityIn);
                stockOut = entries.Sum(x => x.QuantityOut);
                closing = entries.Last().ClosingStock;
            }

            result.Add(new ProductStockSummaryDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                CategoryName = p.Category?.Name ?? "",
                Unit = p.Unit,
                OpeningStock = opening,
                StockIn = stockIn,
                StockOut = stockOut,
                ClosingStock = closing,
                MinimumStock = p.MinimumStock,
                PurchasePrice = p.PurchasePrice,
                SalePrice = p.SalePrice
            });
        }

        return result;
    }

    public async Task<List<StockLedger>> GetProductStockHistoryAsync(int? productId = null, DateTime? startBusinessDate = null, DateTime? endBusinessDate = null)
    {
        var query = _db.StockLedger
            .Include(l => l.Product)
            .Include(l => l.CreatedByUser)
            .AsQueryable();

        if (productId.HasValue && productId.Value > 0)
        {
            query = query.Where(l => l.ProductId == productId.Value);
        }

        if (startBusinessDate.HasValue)
        {
            var s = startBusinessDate.Value.Date;
            query = query.Where(l => l.BusinessDate >= s);
        }

        if (endBusinessDate.HasValue)
        {
            var e = endBusinessDate.Value.Date;
            query = query.Where(l => l.BusinessDate <= e);
        }

        return await query
            .OrderByDescending(l => l.TransactionDateTime)
            .ThenByDescending(l => l.Id)
            .Take(300)
            .ToListAsync();
    }

    public async Task<List<StockAdjustment>> GetAdjustmentsAsync(DateTime? businessDate = null)
    {
        var query = _db.StockAdjustments
            .Include(a => a.Product)
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (businessDate.HasValue)
        {
            var d = businessDate.Value.Date;
            query = query.Where(a => a.BusinessDate == d);
        }

        return await query
            .OrderByDescending(a => a.AdjustmentDateTime)
            .ToListAsync();
    }
}
