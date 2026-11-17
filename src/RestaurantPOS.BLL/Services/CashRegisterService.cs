using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class CashRegisterService : ICashRegisterService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public CashRegisterService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<CashRegister?> GetActiveRegisterAsync(DateTime? businessDate = null)
    {
        var targetDate = (businessDate ?? _businessDayService.GetCurrentBusinessDate()).Date;

        var register = await _db.CashRegisters
            .Include(r => r.OpenedByUser)
            .Include(r => r.ClosedByUser)
            .Include(r => r.Transactions)
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync(r => r.BusinessDate == targetDate);

        if (register != null && !register.IsClosed)
        {
            return await GetRefreshedRegisterSummaryAsync(register.Id);
        }

        return register;
    }

    public async Task<CashRegister> GetRefreshedRegisterSummaryAsync(int registerId)
    {
        var register = await _db.CashRegisters
            .Include(r => r.OpenedByUser)
            .Include(r => r.ClosedByUser)
            .Include(r => r.Transactions)
            .FirstOrDefaultAsync(r => r.Id == registerId)
            ?? throw new InvalidOperationException("Cash register not found.");

        var bDate = register.BusinessDate.Date;

        // Reconcile directly with actual BusinessDate transactions (Rule 10)
        var cashSales = await _db.SalePayments
            .Where(p => p.Sale != null &&
                        p.Sale.BusinessDate == bDate &&
                        p.Sale.Status != SaleStatus.Held &&
                        p.Sale.Status != SaleStatus.Cancelled &&
                        p.PaymentMethod == PaymentMethodType.Cash)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var cashExpenses = await _db.Expenses
            .Where(e => e.BusinessDate == bDate && e.PaymentMethod == PaymentMethodType.Cash)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var cashRefunds = await _db.SalesReturns
            .Where(r => r.BusinessDate == bDate && r.RefundMethod == PaymentMethodType.Cash)
            .SumAsync(r => (decimal?)r.TotalRefundAmount) ?? 0m;

        register.CashSales = cashSales;
        register.CashExpenses = cashExpenses;
        register.CashRefunds = cashRefunds;
        register.CashReceived = cashSales;
        register.ExpectedClosingCash = register.OpeningCash + cashSales - cashExpenses - cashRefunds;

        if (register.ActualClosingCash.HasValue)
        {
            register.CashDifference = register.ActualClosingCash.Value - register.ExpectedClosingCash;
        }

        await _db.SaveChangesAsync();
        return register;
    }

    public async Task<CashRegister> OpenRegisterAsync(OpenCashRegisterDto dto, string userId)
    {
        var nowPkt = _businessDayService.GetNowPakistanTime();
        var businessDate = (dto.BusinessDate ?? _businessDayService.CalculateBusinessDate(nowPkt)).Date;

        await _businessDayService.EnsureBusinessDayRecordAsync(businessDate, userId);

        var existingOpen = await _db.CashRegisters
            .FirstOrDefaultAsync(r => r.BusinessDate == businessDate && !r.IsClosed);
        if (existingOpen != null)
        {
            throw new InvalidOperationException($"A cash register is already open for Business Date {businessDate:dd MMM yyyy}.");
        }

        var register = new CashRegister
        {
            BusinessDate = businessDate,
            OpenedAt = nowPkt,
            OpeningCash = dto.OpeningCash,
            CashSales = 0m,
            CashExpenses = 0m,
            CashRefunds = 0m,
            CashReceived = 0m,
            ExpectedClosingCash = dto.OpeningCash,
            IsClosed = false,
            OpenedByUserId = userId,
            CreatedAt = nowPkt
        };

        _db.CashRegisters.Add(register);
        await _db.SaveChangesAsync();

        _db.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = register.Id,
            BusinessDate = businessDate,
            TransactionDateTime = nowPkt,
            TransactionType = RegisterTransactionType.OpeningCash,
            Amount = dto.OpeningCash,
            ReferenceNumber = $"REG-OPEN-{register.Id:D4}",
            Description = "Opening Cash Float",
            CreatedByUserId = userId,
            CreatedAt = nowPkt
        });

        await _db.SaveChangesAsync();
        return await GetRefreshedRegisterSummaryAsync(register.Id);
    }

    public async Task<CashRegister> CloseRegisterAsync(CloseCashRegisterDto dto, string userId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var register = await GetRefreshedRegisterSummaryAsync(dto.CashRegisterId);
            if (register.IsClosed)
            {
                throw new InvalidOperationException("This cash register is already closed.");
            }

            var nowPkt = _businessDayService.GetNowPakistanTime();

            register.ActualClosingCash = dto.ActualClosingCash;
            register.CashDifference = dto.ActualClosingCash - register.ExpectedClosingCash;
            register.IsClosed = true;
            register.ClosedAt = nowPkt;
            register.ClosedByUserId = userId;
            register.ClosingNotes = dto.ClosingNotes?.Trim();
            register.UpdatedAt = nowPkt;

            // Mark BusinessDay record as closed manually by the cashier/admin (never auto-closed at midnight)
            var bDay = await _db.BusinessDays.FirstOrDefaultAsync(b => b.BusinessDate == register.BusinessDate);
            if (bDay != null)
            {
                bDay.IsClosed = true;
                bDay.ClosedAt = nowPkt;
                bDay.ClosedByUserId = userId;
                bDay.ClosingNotes = dto.ClosingNotes?.Trim();
                bDay.UpdatedAt = nowPkt;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return register;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<CashRegister>> GetRegisterHistoryAsync(int count = 30)
    {
        return await _db.CashRegisters
            .Include(r => r.OpenedByUser)
            .Include(r => r.ClosedByUser)
            .OrderByDescending(r => r.BusinessDate)
            .ThenByDescending(r => r.OpenedAt)
            .Take(count)
            .ToListAsync();
    }
}
