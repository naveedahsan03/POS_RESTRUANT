using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class PurchaseService : IPurchaseService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public PurchaseService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<List<Supplier>> GetSuppliersAsync()
    {
        return await _db.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Supplier> SaveSupplierAsync(Supplier supplier)
    {
        var now = _businessDayService.GetNowPakistanTime();
        if (supplier.Id == 0)
        {
            supplier.CreatedAt = now;
            _db.Suppliers.Add(supplier);
        }
        else
        {
            var existing = await _db.Suppliers.FindAsync(supplier.Id)
                ?? throw new InvalidOperationException("Supplier not found.");
            existing.Name = supplier.Name;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;
            existing.IsActive = supplier.IsActive;
            existing.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();
        return supplier;
    }

    public async Task<Purchase> CreatePurchaseAsync(CreatePurchaseRequestDto request, string userId)
    {
        if (request.Items == null || !request.Items.Any(i => i.Quantity > 0))
        {
            throw new InvalidOperationException("Purchase must contain at least one product item.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var nowPkt = request.PurchaseDate ?? _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);
            await _businessDayService.EnsureBusinessDayRecordAsync(businessDate, userId);

            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId)
                ?? throw new InvalidOperationException("Supplier not found.");

            var countToday = await _db.Purchases.CountAsync(p => p.BusinessDate == businessDate);
            var invoiceNo = $"PUR-{businessDate:yyyyMMdd}-{(countToday + 1):D4}";

            decimal totalAmount = 0m;
            var details = new List<PurchaseDetail>();

            foreach (var item in request.Items.Where(i => i.Quantity > 0))
            {
                var lineTotal = Math.Round(item.Quantity * item.PurchasePrice, 2);
                totalAmount += lineTotal;

                details.Add(new PurchaseDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PurchasePrice = item.PurchasePrice,
                    LineTotal = lineTotal
                });
            }

            var paid = Math.Clamp(request.PaidAmount, 0m, totalAmount);
            var remaining = totalAmount - paid;
            var status = remaining == 0m
                ? PaymentStatus.Paid
                : (paid > 0m ? PaymentStatus.Partial : PaymentStatus.Unpaid);

            var purchase = new Purchase
            {
                PurchaseInvoiceNumber = invoiceNo,
                SupplierId = supplier.Id,
                BusinessDate = businessDate,
                PurchaseDate = nowPkt,
                TotalAmount = totalAmount,
                PaidAmount = paid,
                RemainingPayable = remaining,
                PaymentStatus = status,
                PaymentMethod = request.PaymentMethod,
                Notes = request.Notes,
                CreatedByUserId = userId,
                CreatedAt = nowPkt
            };

            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();

            foreach (var detail in details)
            {
                detail.PurchaseId = purchase.Id;
                _db.PurchaseDetails.Add(detail);

                // Update Product Stock & Purchase Price + Record StockLedger
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == detail.ProductId)
                    ?? throw new InvalidOperationException($"Product ID {detail.ProductId} not found.");

                var opening = product.CurrentStock;
                var closing = opening + detail.Quantity;

                product.CurrentStock = closing;
                if (detail.PurchasePrice > 0)
                {
                    product.PurchasePrice = detail.PurchasePrice;
                }
                product.UpdatedAt = nowPkt;

                _db.StockLedger.Add(new StockLedger
                {
                    ProductId = product.Id,
                    BusinessDate = businessDate,
                    TransactionDateTime = nowPkt,
                    MovementType = StockMovementType.Purchase,
                    ReferenceNumber = purchase.PurchaseInvoiceNumber,
                    ReferenceId = purchase.Id,
                    OpeningStock = opening,
                    QuantityIn = detail.Quantity,
                    QuantityOut = 0,
                    ClosingStock = closing,
                    UnitCost = detail.PurchasePrice,
                    Remarks = $"Purchase from {supplier.Name}",
                    CreatedByUserId = userId,
                    CreatedAt = nowPkt
                });
            }

            // Update Supplier Payable Balance
            if (remaining > 0)
            {
                supplier.CurrentBalance += remaining;
                supplier.UpdatedAt = nowPkt;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return purchase;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Purchase>> GetPurchasesAsync(DateTime? businessDate = null)
    {
        var query = _db.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
            .AsQueryable();

        if (businessDate.HasValue)
        {
            var date = businessDate.Value.Date;
            query = query.Where(p => p.BusinessDate == date);
        }

        return await query.OrderByDescending(p => p.PurchaseDate).ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseWithDetailsAsync(int purchaseId)
    {
        return await _db.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseDetails)
                .ThenInclude(d => d.Product)
            .Include(p => p.PurchaseReturns)
                .ThenInclude(r => r.PurchaseReturnDetails)
            .FirstOrDefaultAsync(p => p.Id == purchaseId);
    }

    public async Task<PurchaseReturn> CreatePurchaseReturnAsync(CreatePurchaseReturnRequestDto request, string userId)
    {
        var validItems = request.Items.Where(i => i.ReturnQuantity > 0).ToList();
        if (!validItems.Any())
        {
            throw new InvalidOperationException("Please enter a return quantity greater than zero for at least one product.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var nowPkt = _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);

            var purchase = await _db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseDetails)
                .Include(p => p.PurchaseReturns)
                    .ThenInclude(r => r.PurchaseReturnDetails)
                .FirstOrDefaultAsync(p => p.Id == request.PurchaseId)
                ?? throw new InvalidOperationException("Original purchase invoice not found.");

            var alreadyReturnedByDetail = purchase.PurchaseReturns
                .SelectMany(r => r.PurchaseReturnDetails)
                .GroupBy(d => d.PurchaseDetailId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ReturnQuantity));

            var countToday = await _db.PurchaseReturns.CountAsync(r => r.BusinessDate == businessDate);
            var returnNo = $"PRET-{businessDate:yyyyMMdd}-{(countToday + 1):D4}";

            var purchaseReturn = new PurchaseReturn
            {
                ReturnNumber = returnNo,
                PurchaseId = purchase.Id,
                SupplierId = purchase.SupplierId,
                BusinessDate = businessDate,
                ReturnDate = nowPkt,
                Reason = request.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAt = nowPkt
            };

            _db.PurchaseReturns.Add(purchaseReturn);
            await _db.SaveChangesAsync();

            decimal totalReturnAmount = 0m;

            foreach (var item in validItems)
            {
                var origDetail = purchase.PurchaseDetails.FirstOrDefault(d => d.Id == item.PurchaseDetailId)
                    ?? throw new InvalidOperationException("Original purchase line item not found.");

                var previouslyReturned = alreadyReturnedByDetail.TryGetValue(origDetail.Id, out var prev) ? prev : 0m;
                var maxReturnable = origDetail.Quantity - previouslyReturned;

                // Rule: Prevent returning more quantity than originally purchased
                if (item.ReturnQuantity > maxReturnable)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {item.ReturnQuantity:N2}. Purchased: {origDetail.Quantity:N2}, Already Returned: {previouslyReturned:N2}, Max Returnable: {maxReturnable:N2}.");
                }

                var product = await _db.Products.FirstAsync(p => p.Id == origDetail.ProductId);
                if (product.CurrentStock < item.ReturnQuantity && !product.AllowNegativeStock)
                {
                    throw new InvalidOperationException($"Insufficient current stock for '{product.Name}' to process purchase return.");
                }

                var lineTotal = Math.Round(item.ReturnQuantity * origDetail.PurchasePrice, 2);
                totalReturnAmount += lineTotal;

                _db.PurchaseReturnDetails.Add(new PurchaseReturnDetail
                {
                    PurchaseReturnId = purchaseReturn.Id,
                    PurchaseDetailId = origDetail.Id,
                    ProductId = origDetail.ProductId,
                    ReturnQuantity = item.ReturnQuantity,
                    PurchasePrice = origDetail.PurchasePrice,
                    ReturnLineTotal = lineTotal
                });

                // Decrease Stock & Write StockLedger
                var opening = product.CurrentStock;
                var closing = opening - item.ReturnQuantity;
                product.CurrentStock = closing;
                product.UpdatedAt = nowPkt;

                _db.StockLedger.Add(new StockLedger
                {
                    ProductId = product.Id,
                    BusinessDate = businessDate,
                    TransactionDateTime = nowPkt,
                    MovementType = StockMovementType.PurchaseReturn,
                    ReferenceNumber = purchaseReturn.ReturnNumber,
                    ReferenceId = purchaseReturn.Id,
                    OpeningStock = opening,
                    QuantityIn = 0,
                    QuantityOut = item.ReturnQuantity,
                    ClosingStock = closing,
                    UnitCost = origDetail.PurchasePrice,
                    Remarks = $"Purchase Return ({purchase.PurchaseInvoiceNumber}): {purchaseReturn.Reason}",
                    CreatedByUserId = userId,
                    CreatedAt = nowPkt
                });
            }

            purchaseReturn.TotalReturnAmount = totalReturnAmount;

            // Update Supplier Payable / Account Balance
            if (purchase.Supplier != null)
            {
                purchase.Supplier.CurrentBalance -= totalReturnAmount;
                purchase.Supplier.UpdatedAt = nowPkt;
            }

            purchase.RemainingPayable = Math.Max(0m, purchase.RemainingPayable - totalReturnAmount);
            if (purchase.RemainingPayable == 0m)
            {
                purchase.PaymentStatus = PaymentStatus.Paid;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return purchaseReturn;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<PurchaseReturn>> GetPurchaseReturnsAsync(DateTime? businessDate = null)
    {
        var query = _db.PurchaseReturns
            .Include(r => r.Purchase)
            .Include(r => r.Supplier)
            .Include(r => r.PurchaseReturnDetails)
                .ThenInclude(d => d.Product)
            .AsQueryable();

        if (businessDate.HasValue)
        {
            var date = businessDate.Value.Date;
            query = query.Where(r => r.BusinessDate == date);
        }

        return await query.OrderByDescending(r => r.ReturnDate).ToListAsync();
    }
}
