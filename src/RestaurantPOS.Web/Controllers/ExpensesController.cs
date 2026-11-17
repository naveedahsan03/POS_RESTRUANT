using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ExpensesController : Controller
{
    private readonly IExpenseService _expenseService;
    private readonly IBusinessDayService _businessDayService;

    public ExpensesController(IExpenseService expenseService, IBusinessDayService businessDayService)
    {
        _expenseService = expenseService;
        _businessDayService = businessDayService;
    }

    public async Task<IActionResult> Index(DateTime? startBusinessDate = null, DateTime? endBusinessDate = null)
    {
        var currentBDate = _businessDayService.GetCurrentBusinessDate();
        var start = startBusinessDate ?? currentBDate.AddDays(-7);
        var end = endBusinessDate ?? currentBDate;

        ViewBag.Categories = await _expenseService.GetExpenseCategoriesAsync();
        ViewBag.StartBusinessDate = start;
        ViewBag.EndBusinessDate = end;
        ViewBag.CurrentBusinessDate = currentBDate;

        var expenses = await _expenseService.GetExpensesAsync(start, end);
        return View(expenses);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExpenseDto dto)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid title, category, and amount.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var expense = await _expenseService.CreateExpenseAsync(dto, userId);
            TempData["Success"] = $"Expense '{expense.Title}' (Rs. {expense.Amount:N2}) recorded under Business Date {expense.BusinessDate:dd MMM yyyy}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(ExpenseCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        await _expenseService.SaveExpenseCategoryAsync(category);
        TempData["Success"] = $"Expense category '{category.Name}' saved.";
        return RedirectToAction(nameof(Index));
    }
}
