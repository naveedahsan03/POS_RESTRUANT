using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.DAL.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BusinessDay> BusinessDays => Set<BusinessDay>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnDetail> SalesReturnDetails => Set<SalesReturnDetail>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseDetail> PurchaseDetails => Set<PurchaseDetail>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnDetail> PurchaseReturnDetails => Set<PurchaseReturnDetail>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<StockLedger> StockLedger => Set<StockLedger>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashRegisterTransaction> CashRegisterTransactions => Set<CashRegisterTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Soft-delete filters for master entities (Financial transactions are NEVER deleted)
        builder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Supplier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<RestaurantTable>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ExpenseCategory>().HasQueryFilter(e => !e.IsDeleted);

        // BusinessDay
        builder.Entity<BusinessDay>(b =>
        {
            b.HasIndex(x => x.BusinessDate).IsUnique();
        });

        // Product & Category
        builder.Entity<Product>(b =>
        {
            b.HasIndex(x => x.Name);
            b.HasIndex(x => x.Barcode);
            b.HasIndex(x => x.CategoryId);
            b.HasOne(x => x.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Sale
        builder.Entity<Sale>(b =>
        {
            b.HasIndex(x => x.InvoiceNumber).IsUnique();
            b.HasIndex(x => x.ClientRequestToken);
            b.HasIndex(x => x.BusinessDate);
            b.HasIndex(x => new { x.BusinessDate, x.Status });
            b.HasIndex(x => x.SaleDateTime);

            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.RestaurantTable)
                .WithMany()
                .HasForeignKey(x => x.RestaurantTableId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SaleDetail
        builder.Entity<SaleDetail>(b =>
        {
            b.HasIndex(x => x.SaleId);
            b.HasIndex(x => x.ProductId);

            b.HasOne(x => x.Sale)
                .WithMany(s => s.SaleDetails)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SalePayment
        builder.Entity<SalePayment>(b =>
        {
            b.HasIndex(x => x.SaleId);
            b.HasOne(x => x.Sale)
                .WithMany(s => s.SalePayments)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SalesReturn
        builder.Entity<SalesReturn>(b =>
        {
            b.HasIndex(x => x.ReturnNumber).IsUnique();
            b.HasIndex(x => x.BusinessDate);
            b.HasIndex(x => x.SaleId);

            b.HasOne(x => x.Sale)
                .WithMany(s => s.SalesReturns)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SalesReturnDetail
        builder.Entity<SalesReturnDetail>(b =>
        {
            b.HasOne(x => x.SalesReturn)
                .WithMany(r => r.SalesReturnDetails)
                .HasForeignKey(x => x.SalesReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.SaleDetail)
                .WithMany()
                .HasForeignKey(x => x.SaleDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Purchase
        builder.Entity<Purchase>(b =>
        {
            b.HasIndex(x => x.PurchaseInvoiceNumber).IsUnique();
            b.HasIndex(x => x.BusinessDate);
            b.HasIndex(x => x.SupplierId);

            b.HasOne(x => x.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PurchaseDetail
        builder.Entity<PurchaseDetail>(b =>
        {
            b.HasOne(x => x.Purchase)
                .WithMany(p => p.PurchaseDetails)
                .HasForeignKey(x => x.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PurchaseReturn
        builder.Entity<PurchaseReturn>(b =>
        {
            b.HasIndex(x => x.ReturnNumber).IsUnique();
            b.HasIndex(x => x.BusinessDate);

            b.HasOne(x => x.Purchase)
                .WithMany(p => p.PurchaseReturns)
                .HasForeignKey(x => x.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PurchaseReturnDetail
        builder.Entity<PurchaseReturnDetail>(b =>
        {
            b.HasOne(x => x.PurchaseReturn)
                .WithMany(r => r.PurchaseReturnDetails)
                .HasForeignKey(x => x.PurchaseReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.PurchaseDetail)
                .WithMany()
                .HasForeignKey(x => x.PurchaseDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Expense
        builder.Entity<Expense>(b =>
        {
            b.HasIndex(x => x.BusinessDate);
            b.HasIndex(x => x.ExpenseCategoryId);

            b.HasOne(x => x.ExpenseCategory)
                .WithMany(c => c.Expenses)
                .HasForeignKey(x => x.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // StockLedger
        builder.Entity<StockLedger>(b =>
        {
            b.HasIndex(x => new { x.ProductId, x.TransactionDateTime });
            b.HasIndex(x => x.BusinessDate);

            b.HasOne(x => x.Product)
                .WithMany(p => p.StockLedgerEntries)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // StockAdjustment
        builder.Entity<StockAdjustment>(b =>
        {
            b.HasIndex(x => x.AdjustmentNumber).IsUnique();
            b.HasIndex(x => x.BusinessDate);

            b.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CashRegister
        builder.Entity<CashRegister>(b =>
        {
            b.HasIndex(x => x.BusinessDate);

            b.HasOne(x => x.OpenedByUser)
                .WithMany()
                .HasForeignKey(x => x.OpenedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CashRegisterTransaction
        builder.Entity<CashRegisterTransaction>(b =>
        {
            b.HasIndex(x => x.CashRegisterId);
            b.HasIndex(x => x.BusinessDate);

            b.HasOne(x => x.CashRegister)
                .WithMany(r => r.Transactions)
                .HasForeignKey(x => x.CashRegisterId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
