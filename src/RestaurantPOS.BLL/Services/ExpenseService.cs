using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public ExpenseService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<List<ExpenseCategory>> GetExpenseCategoriesAsync()
    {
        return await _db.ExpenseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ExpenseCategory> SaveExpenseCategoryAsync(ExpenseCategory category)
    {
        var now = _businessDayService.GetNowPakistanTime();
        if (category.Id == 0)
        {
            category.CreatedAt = now;
            _db.ExpenseCategories.Add(category);
        }
        else
        {
            var existing = await _db.ExpenseCategories.FindAsync(category.Id)
                ?? throw new InvalidOperationException("Expense category not found.");
            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.IsActive = category.IsActive;
            existing.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<Expense> CreateExpenseAsync(CreateExpenseDto dto, string userId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var expenseTime = dto.ExpenseDateTime ?? _businessDayService.GetNowPakistanTime();
            // Centralized BusinessDate rule ensures post-midnight (12 AM - 3:59 AM) expenses belong to previous business date
            var businessDate = _businessDayService.CalculateBusinessDate(expenseTime);
            await _businessDayService.EnsureBusinessDayRecordAsync(businessDate, userId);

            var expense = new Expense
            {
                ExpenseCategoryId = dto.ExpenseCategoryId,
                Title = dto.Title.Trim(),
                Amount = dto.Amount,
                ExpenseDateTime = expenseTime,
                BusinessDate = businessDate,
                Description = dto.Description?.Trim(),
                PaymentMethod = dto.PaymentMethod,
                CreatedByUserId = userId,
                CreatedAt = expenseTime
            };

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            if (dto.PaymentMethod == PaymentMethodType.Cash)
            {
                var openRegister = await _db.CashRegisters
                    .FirstOrDefaultAsync(r => r.BusinessDate == businessDate && !r.IsClosed);
                if (openRegister != null)
                {
                    openRegister.CashExpenses += dto.Amount;
                    openRegister.ExpectedClosingCash = openRegister.OpeningCash + openRegister.CashSales - openRegister.CashExpenses - openRegister.CashRefunds;
                    openRegister.UpdatedAt = expenseTime;

                    _db.CashRegisterTransactions.Add(new CashRegisterTransaction
                    {
                        CashRegisterId = openRegister.Id,
                        BusinessDate = businessDate,
                        TransactionDateTime = expenseTime,
                        TransactionType = RegisterTransactionType.CashExpense,
                        Amount = dto.Amount,
                        ReferenceNumber = $"EXP-{expense.Id:D4}",
                        Description = $"Expense: {expense.Title}",
                        CreatedByUserId = userId,
                        CreatedAt = expenseTime
                    });
                    await _db.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();
            return expense;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Expense>> GetExpensesAsync(DateTime? startBusinessDate = null, DateTime? endBusinessDate = null)
    {
        var query = _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Include(e => e.CreatedByUser)
            .AsQueryable();

        if (startBusinessDate.HasValue)
        {
            var s = startBusinessDate.Value.Date;
            query = query.Where(e => e.BusinessDate >= s);
        }

        if (endBusinessDate.HasValue)
        {
            var end = endBusinessDate.Value.Date;
            query = query.Where(e => e.BusinessDate <= end);
        }

        return await query
            .OrderByDescending(e => e.ExpenseDateTime)
            .ToListAsync();
    }
}
