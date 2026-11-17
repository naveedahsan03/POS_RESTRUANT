using System.ComponentModel.DataAnnotations;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.DTOs;

// ==================== PHASE 2: PRODUCT & CATEGORY DTOs ====================
public class ProductDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product Name is required")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    [Range(0, 9999999)]
    public decimal PurchasePrice { get; set; }

    [Range(0.01, 9999999, ErrorMessage = "Sale price must be greater than zero")]
    public decimal SalePrice { get; set; }

    public decimal CurrentStock { get; set; }

    [Range(0, 9999999)]
    public decimal MinimumStock { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = "Piece";

    [MaxLength(80)]
    public string? Barcode { get; set; }

    public bool IsActive { get; set; } = true;
    public bool AllowNegativeStock { get; set; } = false;
    public bool IsLowStock => CurrentStock <= MinimumStock;
}

// ==================== PHASE 3: POS & SALES DTOs ====================
public class CreateSaleRequestDto
{
    public int? ResumedSaleId { get; set; }
    public string? ClientRequestToken { get; set; }
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public int? RestaurantTableId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercentage { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public decimal ReceivedAmount { get; set; }
    public bool IsHoldOrder { get; set; } = false;
    public string? Notes { get; set; }

    public List<CreateSaleItemDto> Items { get; set; } = new();
    public List<CreateSalePaymentDto> SplitPayments { get; set; } = new();
}

public class CreateSaleItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class CreateSalePaymentDto
{
    public PaymentMethodType PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class SaleReceiptDto
{
    public int SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public DateTime SaleDateTime { get; set; }
    public OrderType OrderType { get; set; }
    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public SaleStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public List<SaleReceiptItemDto> Items { get; set; } = new();
    public List<CreateSalePaymentDto> Payments { get; set; } = new();
}

public class SaleReceiptItemDto
{
    public int SaleDetailId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AlreadyReturnedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

// ==================== PHASE 4: PURCHASE & PURCHASE RETURN DTOs ====================
public class CreatePurchaseRequestDto
{
    [Required]
    public int SupplierId { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public string? Notes { get; set; }
    public List<CreatePurchaseItemDto> Items { get; set; } = new();
}

public class CreatePurchaseItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
}

public class CreatePurchaseReturnRequestDto
{
    [Required]
    public int PurchaseId { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    public List<CreatePurchaseReturnItemDto> Items { get; set; } = new();
}

public class CreatePurchaseReturnItemDto
{
    public int PurchaseDetailId { get; set; }
    public int ProductId { get; set; }
    public decimal ReturnQuantity { get; set; }
}

// ==================== PHASE 5: SALES RETURN DTOs ====================
public class CreateSalesReturnRequestDto
{
    [Required]
    public int SaleId { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    public PaymentMethodType RefundMethod { get; set; } = PaymentMethodType.Cash;
    public List<CreateSalesReturnItemDto> Items { get; set; } = new();
}

public class CreateSalesReturnItemDto
{
    public int SaleDetailId { get; set; }
    public int ProductId { get; set; }
    public decimal ReturnQuantity { get; set; }
}

// ==================== PHASE 6: EXPENSE DTOs ====================
public class CreateExpenseDto
{
    public int Id { get; set; }

    [Required]
    public int ExpenseCategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Range(0.01, 99999999, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    public DateTime? ExpenseDateTime { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
}

// ==================== PHASE 7: INVENTORY & STOCK LEDGER DTOs ====================
public class StockAdjustmentRequestDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    public StockMovementType MovementType { get; set; } = StockMovementType.StockAdjustment; // StockAdjustment or Wastage

    [Required]
    public decimal QuantityChange { get; set; } // Positive to add stock, negative to deduct stock (Wastage is always deducted)

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;
}

public class ProductStockSummaryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal OpeningStock { get; set; }
    public decimal StockIn { get; set; }
    public decimal StockOut { get; set; }
    public decimal ClosingStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
}

// ==================== PHASE 8: DASHBOARD & DAILY SALES REPORT DTOs ====================
public class DailyBusinessReportDto
{
    public DateTime StartBusinessDate { get; set; }
    public DateTime EndBusinessDate { get; set; }
    public DateTime ExactWindowStart { get; set; } // e.g., 17 Nov 2026 6:00 PM
    public DateTime ExactWindowEnd { get; set; }   // e.g., 18 Nov 2026 4:00 AM
    public string FilterPreset { get; set; } = "Today";

    // Core 12 Financial Metrics
    public decimal TotalGrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalTax { get; set; }
    public decimal SalesReturns { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashReceived { get; set; }
    public decimal CardPayments { get; set; }
    public decimal OnlinePayments { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal PurchaseReturnsAmount { get; set; }
    public decimal EstimatedCostOfGoodsSold { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal EstimatedNetProfit { get; set; }

    // Order KPIs
    public int TotalOrders { get; set; }
    public int HeldOrdersCount { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Breakdowns
    public List<ProductSalesBreakdownDto> ProductWiseSales { get; set; } = new();
    public List<CategorySalesBreakdownDto> CategoryWiseSales { get; set; } = new();
    public List<PaymentSalesBreakdownDto> PaymentWiseSales { get; set; } = new();
    public List<HourlySalesBreakdownDto> HourlySales { get; set; } = new();
    public List<ProductDto> LowStockProducts { get; set; } = new();
}

public class ProductSalesBreakdownDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedProfit { get; set; }
}

public class CategorySalesBreakdownDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal PercentageOfSales { get; set; }
}

public class PaymentSalesBreakdownDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class HourlySalesBreakdownDto
{
    public string HourLabel { get; set; } = string.Empty; // e.g. "06:00 PM - 06:59 PM", "12:00 AM - 12:59 AM"
    public int Hour24 { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
}

// ==================== PHASE 9: CASH REGISTER / CLOSING DTOs ====================
public class OpenCashRegisterDto
{
    [Range(0, 99999999)]
    public decimal OpeningCash { get; set; }
    public DateTime? BusinessDate { get; set; }
}

public class CloseCashRegisterDto
{
    [Required]
    public int CashRegisterId { get; set; }

    [Range(0, 99999999)]
    public decimal ActualClosingCash { get; set; }

    [MaxLength(500)]
    public string? ClosingNotes { get; set; }
}
