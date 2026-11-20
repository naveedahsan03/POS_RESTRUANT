using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class SalesReturnService : ISalesReturnService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public SalesReturnService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<SalesReturn> CreateSalesReturnAsync(CreateSalesReturnRequestDto request, string userId)
    {
        var validItems = request.Items.Where(i => i.ReturnQuantity > 0).ToList();
        if (!validItems.Any())
        {
            throw new InvalidOperationException("Select at least one sold item with a return quantity greater than zero.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var nowPkt = _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);
            await _businessDayService.EnsureBusinessDayRecordAsync(businessDate, userId);

            var sale = await _db.Sales
                .Include(s => s.SaleDetails)
                .Include(s => s.SalesReturns)
                    .ThenInclude(r => r.SalesReturnDetails)
                .FirstOrDefaultAsync(s => s.Id == request.SaleId)
                ?? throw new InvalidOperationException("Original sale invoice not found.");

            if (sale.Status == SaleStatus.Held || sale.Status == SaleStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot process a sales return against a held or cancelled order.");
            }

            var alreadyReturnedByDetail = sale.SalesReturns
                .SelectMany(r => r.SalesReturnDetails)
                .GroupBy(d => d.SaleDetailId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ReturnQuantity));

            var countToday = await _db.SalesReturns.CountAsync(r => r.BusinessDate == businessDate);
            var returnNo = $"SRET-{businessDate:yyyyMMdd}-{(countToday + 1):D4}";

            var salesReturn = new SalesReturn
            {
                ReturnNumber = returnNo,
                SaleId = sale.Id,
                OriginalInvoiceNumber = sale.InvoiceNumber,
                BusinessDate = businessDate,
                ReturnDateTime = nowPkt,
                RefundMethod = request.RefundMethod,
                Reason = request.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAt = nowPkt
            };

            _db.SalesReturns.Add(salesReturn);
            await _db.SaveChangesAsync();

            decimal totalRefundAmount = 0m;
            var isNoRefund = request.RefundMethod == PaymentMethodType.NoRefund;

            foreach (var item in validItems)
            {
                var origDetail = sale.SaleDetails.FirstOrDefault(d => d.Id == item.SaleDetailId)
                    ?? throw new InvalidOperationException("Original sale line item not found.");

                var previouslyReturned = alreadyReturnedByDetail.TryGetValue(origDetail.Id, out var prev) ? prev : 0m;
                var maxReturnable = origDetail.Quantity - previouslyReturned;

                // Rule 7 & 9: Prevent returning more quantity than originally sold
                if (item.ReturnQuantity > maxReturnable)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {item.ReturnQuantity:N2} of '{origDetail.ProductName}'. Sold: {origDetail.Quantity:N2}, Already Returned: {previouslyReturned:N2}, Max Returnable: {maxReturnable:N2}.");
                }

                // Calculate net unit price paid after line & proportional invoice discount (0 if No Refund)
                var effectiveUnitPrice = isNoRefund
                    ? 0m
                    : (origDetail.Quantity > 0
                        ? Math.Round(origDetail.LineTotal / origDetail.Quantity, 2)
                        : origDetail.UnitPrice);

                var lineRefund = isNoRefund ? 0m : Math.Round(effectiveUnitPrice * item.ReturnQuantity, 2);
                totalRefundAmount += lineRefund;

                _db.SalesReturnDetails.Add(new SalesReturnDetail
                {
                    SalesReturnId = salesReturn.Id,
                    SaleDetailId = origDetail.Id,
                    ProductId = origDetail.ProductId,
                    ReturnQuantity = item.ReturnQuantity,
                    UnitRefundPrice = effectiveUnitPrice,
                    UnitCostSnapshot = origDetail.UnitCostSnapshot,
                    RefundLineTotal = lineRefund
                });

                // Automatically increase stock & write StockLedger entry
                var product = await _db.Products.FirstAsync(p => p.Id == origDetail.ProductId);
                var opening = product.CurrentStock;
                var closing = opening + item.ReturnQuantity;
                product.CurrentStock = closing;
                product.UpdatedAt = nowPkt;

                _db.StockLedger.Add(new StockLedger
                {
                    ProductId = product.Id,
                    BusinessDate = businessDate,
                    TransactionDateTime = nowPkt,
                    MovementType = StockMovementType.SalesReturn,
                    ReferenceNumber = salesReturn.ReturnNumber,
                    ReferenceId = salesReturn.Id,
                    OpeningStock = opening,
                    QuantityIn = item.ReturnQuantity,
                    QuantityOut = 0,
                    ClosingStock = closing,
                    UnitCost = origDetail.UnitCostSnapshot,
                    Remarks = isNoRefund
                        ? $"Sales Return (No Refund) against {sale.InvoiceNumber}: {salesReturn.Reason}"
                        : $"Sales Return against {sale.InvoiceNumber}: {salesReturn.Reason}",
                    CreatedByUserId = userId,
                    CreatedAt = nowPkt
                });
            }

            salesReturn.TotalRefundAmount = totalRefundAmount;

            // Update original sale status (PartiallyReturned or Returned)
            var totalSoldQty = sale.SaleDetails.Sum(d => d.Quantity);
            var totalReturnedQtyAfterThis = alreadyReturnedByDetail.Values.Sum() + validItems.Sum(i => i.ReturnQuantity);
            sale.Status = totalReturnedQtyAfterThis >= totalSoldQty ? SaleStatus.Returned : SaleStatus.PartiallyReturned;
            sale.UpdatedAt = nowPkt;

            // If refunded via Cash, update open CashRegister for this BusinessDate
            if (request.RefundMethod == PaymentMethodType.Cash && totalRefundAmount > 0)
            {
                var openRegister = await _db.CashRegisters
                    .FirstOrDefaultAsync(r => r.BusinessDate == businessDate && !r.IsClosed);
                if (openRegister != null)
                {
                    openRegister.CashRefunds += totalRefundAmount;
                    openRegister.ExpectedClosingCash = openRegister.OpeningCash + openRegister.CashSales - openRegister.CashExpenses - openRegister.CashRefunds;
                    openRegister.UpdatedAt = nowPkt;

                    _db.CashRegisterTransactions.Add(new CashRegisterTransaction
                    {
                        CashRegisterId = openRegister.Id,
                        BusinessDate = businessDate,
                        TransactionDateTime = nowPkt,
                        TransactionType = RegisterTransactionType.CashRefund,
                        Amount = totalRefundAmount,
                        ReferenceNumber = salesReturn.ReturnNumber,
                        Description = $"Cash Refund for {sale.InvoiceNumber}",
                        CreatedByUserId = userId,
                        CreatedAt = nowPkt
                    });
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return salesReturn;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<SalesReturn>> GetSalesReturnsAsync(DateTime? businessDate = null)
    {
        var query = _db.SalesReturns
            .Include(r => r.CreatedByUser)
            .Include(r => r.SalesReturnDetails)
                .ThenInclude(d => d.Product)
            .AsQueryable();

        if (businessDate.HasValue)
        {
            var date = businessDate.Value.Date;
            query = query.Where(r => r.BusinessDate == date);
        }

        return await query.OrderByDescending(r => r.ReturnDateTime).ToListAsync();
    }
}
