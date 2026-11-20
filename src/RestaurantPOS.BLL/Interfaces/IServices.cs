using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.BLL.Interfaces;

public interface IBusinessDayService
{
    DateTime GetNowPakistanTime();
    DateTime CalculateBusinessDate(DateTime transactionDateTimePkt);
    DateTime GetCurrentBusinessDate();
    (DateTime WindowStart, DateTime WindowEnd) GetBusinessDayWindow(DateTime businessDate);
    (DateTime WindowStart, DateTime WindowEnd) GetBusinessDateRangeWindow(DateTime startBusinessDate, DateTime endBusinessDate);
    Task<BusinessDay> EnsureBusinessDayRecordAsync(DateTime businessDate, string? userId = null);
}

public interface IProductService
{
    Task<List<Category>> GetCategoriesAsync(bool includeInactive = false);
    Task<Category> SaveCategoryAsync(Category model);
    Task DeleteCategoryAsync(int id);

    Task<List<ProductDto>> GetProductsAsync(string? search = null, int? categoryId = null, bool lowStockOnly = false, bool activeOnly = false);
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<Product> CreateProductAsync(ProductDto dto, string userId);
    Task<Product> UpdateProductAsync(ProductDto dto, string userId);
    Task SoftDeleteProductAsync(int id);
}

public interface IPosSaleService
{
    Task<SaleReceiptDto> CreateOrCompleteSaleAsync(CreateSaleRequestDto request, string userId, bool isAdmin);
    Task<List<Sale>> GetHeldOrdersAsync();
    Task CancelHeldOrderAsync(int saleId, string userId);
    Task<SaleReceiptDto?> GetSaleReceiptAsync(int saleId);
    Task<SaleReceiptDto?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
    Task<List<Sale>> GetRecentSalesAsync(DateTime? businessDate = null, int count = 50);
    Task<List<RestaurantTable>> GetTablesAsync();
    Task UpdateTableStatusAsync(int tableId, RestaurantPOS.DAL.Enums.TableStatus status);
}

public interface IPurchaseService
{
    Task<List<Supplier>> GetSuppliersAsync();
    Task<Supplier> SaveSupplierAsync(Supplier supplier);
    Task<Purchase> CreatePurchaseAsync(CreatePurchaseRequestDto request, string userId);
    Task<List<Purchase>> GetPurchasesAsync(DateTime? businessDate = null);
    Task<Purchase?> GetPurchaseWithDetailsAsync(int purchaseId);
    Task<PurchaseReturn> CreatePurchaseReturnAsync(CreatePurchaseReturnRequestDto request, string userId);
    Task<List<PurchaseReturn>> GetPurchaseReturnsAsync(DateTime? businessDate = null);
}

public interface ISalesReturnService
{
    Task<SalesReturn> CreateSalesReturnAsync(CreateSalesReturnRequestDto request, string userId);
    Task<List<SalesReturn>> GetSalesReturnsAsync(DateTime? businessDate = null);
}

public interface IExpenseService
{
    Task<List<ExpenseCategory>> GetExpenseCategoriesAsync();
    Task<ExpenseCategory> SaveExpenseCategoryAsync(ExpenseCategory category);
    Task<Expense> CreateExpenseAsync(CreateExpenseDto dto, string userId);
    Task<List<Expense>> GetExpensesAsync(DateTime? startBusinessDate = null, DateTime? endBusinessDate = null);
}

public interface IInventoryService
{
    Task<StockAdjustment> AdjustStockAsync(StockAdjustmentRequestDto request, string userId, bool isAdmin);
    Task<List<ProductStockSummaryDto>> GetStockSummaryAsync(DateTime? startBusinessDate = null, DateTime? endBusinessDate = null);
    Task<List<StockLedger>> GetProductStockHistoryAsync(int? productId = null, DateTime? startBusinessDate = null, DateTime? endBusinessDate = null);
    Task<List<StockAdjustment>> GetAdjustmentsAsync(DateTime? businessDate = null);
}

public interface IReportService
{
    Task<DailyBusinessReportDto> GetDailyBusinessReportAsync(DateTime startBusinessDate, DateTime endBusinessDate, string preset = "Custom");
    Task<byte[]> ExportDailyReportToExcelAsync(DailyBusinessReportDto report);
}

public interface ICashRegisterService
{
    Task<CashRegister?> GetActiveRegisterAsync(DateTime? businessDate = null);
    Task<CashRegister> GetRefreshedRegisterSummaryAsync(int registerId);
    Task<CashRegister> OpenRegisterAsync(OpenCashRegisterDto dto, string userId);
    Task<CashRegister> CloseRegisterAsync(CloseCashRegisterDto dto, string userId);
    Task<List<CashRegister>> GetRegisterHistoryAsync(int count = 30);
}
