using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.DAL.Entities;

public abstract class BaseAuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(5);
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
}

// 1. Users (extends ASP.NET Core IdentityUser)
public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string RoleDisplay { get; set; } = "Cashier";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(5);
}

// 2. BusinessDays
public class BusinessDay : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime WindowStart { get; set; } // BusinessDate 18:00:00 PKT
    public DateTime WindowEnd { get; set; }   // BusinessDate + 1 Day 04:00:00 PKT

    public bool IsClosed { get; set; } = false;
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    [MaxLength(450)]
    public string? OpenedByUserId { get; set; }

    [MaxLength(450)]
    public string? ClosedByUserId { get; set; }

    [MaxLength(500)]
    public string? ClosingNotes { get; set; }
}

// 3. Categories
public class Category : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

// 4. Products
public class Product : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalePrice { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal CurrentStock { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal MinimumStock { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = "Piece"; // Piece, Plate, KG, Liter, Portion

    [MaxLength(80)]
    public string? Barcode { get; set; }

    public bool IsActive { get; set; } = true;
    public bool AllowNegativeStock { get; set; } = false;

    public ICollection<StockLedger> StockLedgerEntries { get; set; } = new List<StockLedger>();
}

// 5. Suppliers
public class Supplier : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentBalance { get; set; } // Remaining payable balance

    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}

// 6. Customers
public class Customer : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}

// 7. RestaurantTables
public class RestaurantTable : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string TableNumber { get; set; } = string.Empty;

    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public bool IsActive { get; set; } = true;
}

// 8. Sales
public class Sale : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ClientRequestToken { get; set; } // Prevents duplicate invoice submissions

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime SaleDateTime { get; set; }

    public OrderType OrderType { get; set; } = OrderType.DineIn;

    public int? RestaurantTableId { get; set; }
    public RestaurantTable? RestaurantTable { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(120)]
    public string? CustomerName { get; set; }

    [MaxLength(30)]
    public string? CustomerPhone { get; set; }

    [MaxLength(250)]
    public string? DeliveryAddress { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; } // Grand Total

    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ChangeAmount { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    [MaxLength(300)]
    public string? Notes { get; set; }

    public ICollection<SaleDetail> SaleDetails { get; set; } = new List<SaleDetail>();
    public ICollection<SalePayment> SalePayments { get; set; } = new List<SalePayment>();
    public ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
}

// 9. SaleDetails
public class SaleDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(150)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCostSnapshot { get; set; } // Captures purchase cost at time of sale for accurate COGS

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

// 10. SalePayments (Supports single and Split Payments)
public class SalePayment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public PaymentMethodType PaymentMethod { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow.AddHours(5);
}

// 11. SalesReturns
public class SalesReturn : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required]
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    [Required, MaxLength(40)]
    public string OriginalInvoiceNumber { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime ReturnDateTime { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalRefundAmount { get; set; }

    public PaymentMethodType RefundMethod { get; set; } = PaymentMethodType.Cash;

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<SalesReturnDetail> SalesReturnDetails { get; set; } = new List<SalesReturnDetail>();
}

// 12. SalesReturnDetails
public class SalesReturnDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }

    [Required]
    public int SaleDetailId { get; set; }
    public SaleDetail? SaleDetail { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReturnQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitRefundPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCostSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundLineTotal { get; set; }
}

// 13. Purchases
public class Purchase : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string PurchaseInvoiceNumber { get; set; } = string.Empty;

    [Required]
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime PurchaseDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingPayable { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Paid;
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;

    [MaxLength(300)]
    public string? Notes { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<PurchaseDetail> PurchaseDetails { get; set; } = new List<PurchaseDetail>();
    public ICollection<PurchaseReturn> PurchaseReturns { get; set; } = new List<PurchaseReturn>();
}

// 14. PurchaseDetails
public class PurchaseDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

// 15. PurchaseReturns
public class PurchaseReturn : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required]
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    [Required]
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime ReturnDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalReturnAmount { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<PurchaseReturnDetail> PurchaseReturnDetails { get; set; } = new List<PurchaseReturnDetail>();
}

// 16. PurchaseReturnDetails
public class PurchaseReturnDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }

    [Required]
    public int PurchaseDetailId { get; set; }
    public PurchaseDetail? PurchaseDetail { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReturnQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReturnLineTotal { get; set; }
}

// 17. ExpenseCategories
public class ExpenseCategory : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

// 18. Expenses
public class Expense : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime ExpenseDateTime { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }
}

// 19. StockLedger
public class StockLedger : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime TransactionDateTime { get; set; }

    public StockMovementType MovementType { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    public int? ReferenceId { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal OpeningStock { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityIn { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityOut { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ClosingStock { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [MaxLength(300)]
    public string? Remarks { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }
}

// 20. StockAdjustments
public class StockAdjustment : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string AdjustmentNumber { get; set; } = string.Empty;

    [Required]
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime AdjustmentDateTime { get; set; }

    public StockMovementType MovementType { get; set; } = StockMovementType.StockAdjustment; // StockAdjustment or Wastage

    [Column(TypeName = "decimal(18,3)")]
    public decimal PreviousStock { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal AdjustedQuantity { get; set; } // Positive for addition, negative for deduction/wastage

    [Column(TypeName = "decimal(18,3)")]
    public decimal NewStock { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }
}

// 21. CashRegisters
public class CashRegister : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashSales { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashExpenses { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashRefunds { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashReceived { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedClosingCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualClosingCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CashDifference { get; set; }

    public bool IsClosed { get; set; } = false;

    [Required, MaxLength(450)]
    public string OpenedByUserId { get; set; } = string.Empty;
    public ApplicationUser? OpenedByUser { get; set; }

    [MaxLength(450)]
    public string? ClosedByUserId { get; set; }
    public ApplicationUser? ClosedByUser { get; set; }

    [MaxLength(500)]
    public string? ClosingNotes { get; set; }

    public ICollection<CashRegisterTransaction> Transactions { get; set; } = new List<CashRegisterTransaction>();
}

// 22. CashRegisterTransactions
public class CashRegisterTransaction : BaseAuditableEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CashRegisterId { get; set; }
    public CashRegister? CashRegister { get; set; }

    [Column(TypeName = "date")]
    public DateTime BusinessDate { get; set; }

    public DateTime TransactionDateTime { get; set; }

    public RegisterTransactionType TransactionType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } // Positive for cash in, negative or directional by type

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
}
