using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class PosSaleService : IPosSaleService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public PosSaleService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<SaleReceiptDto> CreateOrCompleteSaleAsync(CreateSaleRequestDto request, string userId, bool isAdmin)
    {
        if (request.Items == null || !request.Items.Any(i => i.Quantity > 0))
        {
            throw new InvalidOperationException("Cannot save an order with no items.");
        }

        // Rule 5: Prevent duplicate invoice submissions via idempotency token
        if (!string.IsNullOrWhiteSpace(request.ClientRequestToken))
        {
            var existingByToken = await _db.Sales
                .FirstOrDefaultAsync(s => s.ClientRequestToken == request.ClientRequestToken);
            if (existingByToken != null)
            {
                return (await GetSaleReceiptAsync(existingByToken.Id))!;
            }
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var nowPkt = _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(nowPkt);
            await _businessDayService.EnsureBusinessDayRecordAsync(businessDate, userId);

            Sale sale;
            int? previousTableId = null;
            if (request.ResumedSaleId.HasValue && request.ResumedSaleId.Value > 0)
            {
                sale = await _db.Sales
                    .Include(s => s.SaleDetails)
                    .Include(s => s.SalePayments)
                    .FirstOrDefaultAsync(s => s.Id == request.ResumedSaleId.Value && s.Status == SaleStatus.Held)
                    ?? throw new InvalidOperationException("Held order not found or already completed.");

                previousTableId = sale.RestaurantTableId;
                _db.SaleDetails.RemoveRange(sale.SaleDetails);
                _db.SalePayments.RemoveRange(sale.SalePayments);
                sale.UpdatedAt = nowPkt;
            }
            else
            {
                var datePrefix = businessDate.ToString("yyyyMMdd");
                var todayCount = await _db.Sales.CountAsync(s => s.BusinessDate == businessDate);
                var invoiceNo = $"INV-{datePrefix}-{(todayCount + 1):D4}";

                // Ensure uniqueness in high-concurrency scenarios
                while (await _db.Sales.AnyAsync(s => s.InvoiceNumber == invoiceNo))
                {
                    todayCount++;
                    invoiceNo = $"INV-{datePrefix}-{(todayCount + 1):D4}";
                }

                sale = new Sale
                {
                    InvoiceNumber = invoiceNo,
                    ClientRequestToken = request.ClientRequestToken,
                    CreatedAt = nowPkt
                };
                _db.Sales.Add(sale);
            }

            sale.BusinessDate = businessDate;
            sale.SaleDateTime = nowPkt;
            sale.OrderType = request.OrderType;
            sale.RestaurantTableId = request.OrderType == OrderType.DineIn ? request.RestaurantTableId : null;
            sale.CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Walk-In Customer" : request.CustomerName.Trim();
            sale.CustomerPhone = request.CustomerPhone?.Trim();
            sale.DeliveryAddress = request.DeliveryAddress?.Trim();
            sale.UserId = userId;
            sale.Notes = request.Notes;
            sale.Status = request.IsHoldOrder ? SaleStatus.Held : SaleStatus.Completed;

            decimal subTotal = 0m;
            var saleDetails = new List<SaleDetail>();

            foreach (var item in request.Items.Where(i => i.Quantity > 0))
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.IsActive)
                    ?? throw new InvalidOperationException($"Product ID {item.ProductId} is inactive or does not exist.");
                //if (!request.IsHoldOrder && product.CurrentStock < item.Quantity && !product.AllowNegativeStock && !isAdmin)
                //{
                //    throw new InvalidOperationException(
                //        $"Insufficient stock for '{product.Name}'. Available: {product.CurrentStock:N2} {product.Unit}, Requested: {item.Quantity:N2}.");
                //}
                var grossLine = Math.Round(product.SalePrice * item.Quantity, 2);
                var lineDiscount = Math.Clamp(item.DiscountAmount, 0m, grossLine);
                var netLine = grossLine - lineDiscount;

                subTotal += netLine;

                saleDetails.Add(new SaleDetail
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = product.SalePrice,
                    UnitCostSnapshot = product.PurchasePrice,
                    DiscountAmount = lineDiscount,
                    LineTotal = netLine
                });
            }

            // Rule 4: All financial calculations happen on the server
            var orderDiscount = Math.Clamp(request.DiscountAmount, 0m, subTotal);
            var taxableAmount = subTotal - orderDiscount;
            var taxPct = Math.Clamp(request.TaxPercentage, 0m, 100m);
            var taxAmount = Math.Round(taxableAmount * (taxPct / 100m), 2);
            var grandTotal = Math.Round(taxableAmount + taxAmount, 2);

            sale.SubTotal = subTotal;
            sale.DiscountAmount = orderDiscount;
            sale.TaxPercentage = taxPct;
            sale.TaxAmount = taxAmount;
            sale.TotalAmount = grandTotal;
            sale.PaymentMethod = request.PaymentMethod;

            await _db.SaveChangesAsync();

            foreach (var detail in saleDetails)
            {
                detail.SaleId = sale.Id;
                _db.SaleDetails.Add(detail);
            }

            if (previousTableId.HasValue && previousTableId != sale.RestaurantTableId)
            {
                var otherHeldOnPrev = await _db.Sales.AnyAsync(s =>
                    s.Id != sale.Id && s.Status == SaleStatus.Held && s.RestaurantTableId == previousTableId.Value);
                if (!otherHeldOnPrev)
                {
                    var prevTable = await _db.RestaurantTables.FindAsync(previousTableId.Value);
                    if (prevTable != null) prevTable.Status = TableStatus.Available;
                }
            }

            if (request.IsHoldOrder)
            {
                sale.ReceivedAmount = 0;
                sale.ChangeAmount = 0;

                if (sale.OrderType == OrderType.DineIn && sale.RestaurantTableId.HasValue)
                {
                    var table = await _db.RestaurantTables.FindAsync(sale.RestaurantTableId.Value);
                    if (table != null) table.Status = TableStatus.Occupied;
                }
            }
            else
            {
                // Completed Sale -> Process Payments, Deduct Inventory, Write StockLedger, Update CashRegister
                decimal cashPortionForRegister = 0m;

                if (request.PaymentMethod == PaymentMethodType.Split && request.SplitPayments.Any())
                {
                    var totalSplitPaid = request.SplitPayments.Sum(p => p.Amount);
                    if (totalSplitPaid < grandTotal)
                    {
                        throw new InvalidOperationException($"Split payment total (Rs. {totalSplitPaid:N2}) is less than Grand Total (Rs. {grandTotal:N2}).");
                    }

                    sale.ReceivedAmount = totalSplitPaid;
                    sale.ChangeAmount = totalSplitPaid - grandTotal;

                    foreach (var sp in request.SplitPayments.Where(p => p.Amount > 0))
                    {
                        _db.SalePayments.Add(new SalePayment
                        {
                            SaleId = sale.Id,
                            PaymentMethod = sp.PaymentMethod,
                            Amount = sp.Amount,
                            ReferenceNumber = sp.ReferenceNumber,
                            PaidAt = nowPkt
                        });
                    }

                    var rawCash = request.SplitPayments.Where(p => p.PaymentMethod == PaymentMethodType.Cash).Sum(p => p.Amount);
                    cashPortionForRegister = Math.Max(0m, rawCash - sale.ChangeAmount);
                }
                else
                {
                    var received = request.ReceivedAmount >= grandTotal ? request.ReceivedAmount : grandTotal;
                    sale.ReceivedAmount = received;
                    sale.ChangeAmount = received - grandTotal;

                    _db.SalePayments.Add(new SalePayment
                    {
                        SaleId = sale.Id,
                        PaymentMethod = request.PaymentMethod,
                        Amount = grandTotal,
                        PaidAt = nowPkt
                    });

                    if (request.PaymentMethod == PaymentMethodType.Cash)
                    {
                        cashPortionForRegister = grandTotal;
                    }
                }

                // Deduct stock and create StockLedger entry for every item
                foreach (var detail in saleDetails)
                {
                    var product = await _db.Products.FirstAsync(p => p.Id == detail.ProductId);
                    var opening = product.CurrentStock;
                    var closing = opening - detail.Quantity;
                    product.CurrentStock = closing;
                    product.UpdatedAt = nowPkt;

                    _db.StockLedger.Add(new StockLedger
                    {
                        ProductId = product.Id,
                        BusinessDate = businessDate,
                        TransactionDateTime = nowPkt,
                        MovementType = StockMovementType.Sale,
                        ReferenceNumber = sale.InvoiceNumber,
                        ReferenceId = sale.Id,
                        OpeningStock = opening,
                        QuantityIn = 0,
                        QuantityOut = detail.Quantity,
                        ClosingStock = closing,
                        UnitCost = detail.UnitCostSnapshot,
                        Remarks = $"POS Sale ({sale.OrderType})",
                        CreatedByUserId = userId,
                        CreatedAt = nowPkt
                    });
                }

                // Release table if DineIn order is completed and paid
                if (sale.OrderType == OrderType.DineIn && sale.RestaurantTableId.HasValue)
                {
                    var table = await _db.RestaurantTables.FindAsync(sale.RestaurantTableId.Value);
                    if (table != null) table.Status = TableStatus.Available;
                }

                // Record Cash Register transaction if an open register exists for this BusinessDate
                if (cashPortionForRegister > 0)
                {
                    var openRegister = await _db.CashRegisters
                        .FirstOrDefaultAsync(r => r.BusinessDate == businessDate && !r.IsClosed);
                    if (openRegister != null)
                    {
                        openRegister.CashSales += cashPortionForRegister;
                        openRegister.CashReceived += cashPortionForRegister;
                        openRegister.ExpectedClosingCash = openRegister.OpeningCash + openRegister.CashSales - openRegister.CashExpenses - openRegister.CashRefunds;
                        openRegister.UpdatedAt = nowPkt;

                        _db.CashRegisterTransactions.Add(new CashRegisterTransaction
                        {
                            CashRegisterId = openRegister.Id,
                            BusinessDate = businessDate,
                            TransactionDateTime = nowPkt,
                            TransactionType = RegisterTransactionType.CashSale,
                            Amount = cashPortionForRegister,
                            ReferenceNumber = sale.InvoiceNumber,
                            Description = $"Cash Sale {sale.InvoiceNumber}",
                            CreatedByUserId = userId,
                            CreatedAt = nowPkt
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (await GetSaleReceiptAsync(sale.Id))!;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Sale>> GetHeldOrdersAsync()
    {
        return await _db.Sales
            .Include(s => s.SaleDetails)
            .Include(s => s.RestaurantTable)
            .Where(s => s.Status == SaleStatus.Held)
            .OrderByDescending(s => s.SaleDateTime)
            .ToListAsync();
    }

    public async Task CancelHeldOrderAsync(int saleId, string userId)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == saleId && s.Status == SaleStatus.Held)
            ?? throw new InvalidOperationException("Held order not found or already completed.");

        var nowPkt = _businessDayService.GetNowPakistanTime();
        sale.Status = SaleStatus.Cancelled;
        sale.UpdatedAt = nowPkt;

        if (sale.OrderType == OrderType.DineIn && sale.RestaurantTableId.HasValue)
        {
            var otherHeldOnTable = await _db.Sales.AnyAsync(s =>
                s.Id != sale.Id && s.Status == SaleStatus.Held && s.RestaurantTableId == sale.RestaurantTableId.Value);
            if (!otherHeldOnTable)
            {
                var table = await _db.RestaurantTables.FindAsync(sale.RestaurantTableId.Value);
                if (table != null)
                {
                    table.Status = TableStatus.Available;
                    table.UpdatedAt = nowPkt;
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<SaleReceiptDto?> GetSaleReceiptAsync(int saleId)
    {
        var sale = await _db.Sales
            .Include(s => s.SaleDetails)
            .Include(s => s.SalePayments)
            .Include(s => s.RestaurantTable)
            .Include(s => s.User)
            .Include(s => s.SalesReturns)
                .ThenInclude(r => r.SalesReturnDetails)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        return sale == null ? null : MapToReceiptDto(sale);
    }

    public async Task<SaleReceiptDto?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;
        var clean = invoiceNumber.Trim();

        var sale = await _db.Sales
            .Include(s => s.SaleDetails)
            .Include(s => s.SalePayments)
            .Include(s => s.RestaurantTable)
            .Include(s => s.User)
            .Include(s => s.SalesReturns)
                .ThenInclude(r => r.SalesReturnDetails)
            .FirstOrDefaultAsync(s => s.InvoiceNumber == clean);

        return sale == null ? null : MapToReceiptDto(sale);
    }

    private static SaleReceiptDto MapToReceiptDto(Sale sale)
    {
        var returnedByDetailId = sale.SalesReturns
            .SelectMany(r => r.SalesReturnDetails)
            .GroupBy(d => d.SaleDetailId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ReturnQuantity));

        return new SaleReceiptDto
        {
            SaleId = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            BusinessDate = sale.BusinessDate,
            SaleDateTime = sale.SaleDateTime,
            OrderType = sale.OrderType,
            RestaurantTableId = sale.RestaurantTableId,
            TableNumber = sale.RestaurantTable?.TableNumber,
            CustomerName = sale.CustomerName,
            CustomerPhone = sale.CustomerPhone,
            DeliveryAddress = sale.DeliveryAddress,
            CashierName = sale.User?.FullName ?? "Cashier",
            Status = sale.Status,
            SubTotal = sale.SubTotal,
            DiscountAmount = sale.DiscountAmount,
            TaxPercentage = sale.TaxPercentage,
            TaxAmount = sale.TaxAmount,
            TotalAmount = sale.TotalAmount,
            PaymentMethod = sale.PaymentMethod,
            ReceivedAmount = sale.ReceivedAmount,
            ChangeAmount = sale.ChangeAmount,
            Items = sale.SaleDetails.Select(d => new SaleReceiptItemDto
            {
                SaleDetailId = d.Id,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                AlreadyReturnedQuantity = returnedByDetailId.TryGetValue(d.Id, out var retQty) ? retQty : 0m,
                UnitPrice = d.UnitPrice,
                DiscountAmount = d.DiscountAmount,
                LineTotal = d.LineTotal
            }).ToList(),
            Payments = sale.SalePayments.Select(p => new CreateSalePaymentDto
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                ReferenceNumber = p.ReferenceNumber
            }).ToList()
        };
    }

    public async Task<List<Sale>> GetRecentSalesAsync(DateTime? businessDate = null, int count = 50)
    {
        var query = _db.Sales
            .Include(s => s.RestaurantTable)
            .Include(s => s.User)
            .AsQueryable();

        if (businessDate.HasValue)
        {
            var target = businessDate.Value.Date;
            query = query.Where(s => s.BusinessDate == target);
        }

        return await query
            .OrderByDescending(s => s.SaleDateTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<RestaurantTable>> GetTablesAsync()
    {
        return await _db.RestaurantTables
            .Where(t => t.IsActive)
            .OrderBy(t => t.TableNumber)
            .ToListAsync();
    }

    public async Task UpdateTableStatusAsync(int tableId, TableStatus status)
    {
        var table = await _db.RestaurantTables.FindAsync(tableId)
            ?? throw new InvalidOperationException("Table not found.");
        table.Status = status;
        table.UpdatedAt = _businessDayService.GetNowPakistanTime();
        await _db.SaveChangesAsync();
    }
}
