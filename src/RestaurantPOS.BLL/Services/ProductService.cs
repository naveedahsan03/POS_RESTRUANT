using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.DTOs;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.BLL.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessDayService _businessDayService;

    public ProductService(ApplicationDbContext db, IBusinessDayService businessDayService)
    {
        _db = db;
        _businessDayService = businessDayService;
    }

    public async Task<List<Category>> GetCategoriesAsync(bool includeInactive = false)
    {
        var query = _db.Categories.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        return await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();
    }

    public async Task<Category> SaveCategoryAsync(Category model)
    {
        var now = _businessDayService.GetNowPakistanTime();
        if (model.Id == 0)
        {
            model.CreatedAt = now;
            _db.Categories.Add(model);
        }
        else
        {
            var existing = await _db.Categories.FindAsync(model.Id)
                ?? throw new InvalidOperationException("Category not found.");
            existing.Name = model.Name;
            existing.Description = model.Description;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return model;
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var cat = await _db.Categories.FindAsync(id)
            ?? throw new InvalidOperationException("Category not found.");
        cat.IsDeleted = true;
        cat.IsActive = false;
        cat.UpdatedAt = _businessDayService.GetNowPakistanTime();
        await _db.SaveChangesAsync();
    }

    public async Task<List<ProductDto>> GetProductsAsync(string? search = null, int? categoryId = null, bool lowStockOnly = false, bool activeOnly = false)
    {
        var query = _db.Products.Include(p => p.Category).AsQueryable();

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (lowStockOnly)
        {
            query = query.Where(p => p.CurrentStock <= p.MinimumStock);
        }

        return await query
            .OrderBy(p => p.Category!.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "",
                PurchasePrice = p.PurchasePrice,
                SalePrice = p.SalePrice,
                CurrentStock = p.CurrentStock,
                MinimumStock = p.MinimumStock,
                Unit = p.Unit,
                Barcode = p.Barcode,
                IsActive = p.IsActive,
                AllowNegativeStock = p.AllowNegativeStock
            })
            .ToListAsync();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        var p = await _db.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return null;

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? "",
            PurchasePrice = p.PurchasePrice,
            SalePrice = p.SalePrice,
            CurrentStock = p.CurrentStock,
            MinimumStock = p.MinimumStock,
            Unit = p.Unit,
            Barcode = p.Barcode,
            IsActive = p.IsActive,
            AllowNegativeStock = p.AllowNegativeStock
        };
    }

    public async Task<Product> CreateProductAsync(ProductDto dto, string userId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = _businessDayService.GetNowPakistanTime();
            var businessDate = _businessDayService.CalculateBusinessDate(now);

            var product = new Product
            {
                Name = dto.Name.Trim(),
                CategoryId = dto.CategoryId,
                PurchasePrice = dto.PurchasePrice,
                SalePrice = dto.SalePrice,
                CurrentStock = dto.CurrentStock,
                MinimumStock = dto.MinimumStock,
                Unit = dto.Unit,
                Barcode = dto.Barcode,
                IsActive = dto.IsActive,
                AllowNegativeStock = dto.AllowNegativeStock,
                CreatedAt = now
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // Always create OpeningStock ledger record so StockLedger reconciles 100%
            _db.StockLedger.Add(new StockLedger
            {
                ProductId = product.Id,
                BusinessDate = businessDate,
                TransactionDateTime = now,
                MovementType = StockMovementType.OpeningStock,
                ReferenceNumber = $"OPEN-{product.Id:D4}",
                ReferenceId = product.Id,
                OpeningStock = 0,
                QuantityIn = product.CurrentStock > 0 ? product.CurrentStock : 0,
                QuantityOut = 0,
                ClosingStock = product.CurrentStock,
                UnitCost = product.PurchasePrice,
                Remarks = "Opening stock at product creation",
                CreatedByUserId = userId,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return product;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Product> UpdateProductAsync(ProductDto dto, string userId)
    {
        var existing = await _db.Products.FindAsync(dto.Id)
            ?? throw new InvalidOperationException("Product not found.");

        existing.Name = dto.Name.Trim();
        existing.CategoryId = dto.CategoryId;
        existing.PurchasePrice = dto.PurchasePrice;
        existing.SalePrice = dto.SalePrice;
        existing.MinimumStock = dto.MinimumStock;
        existing.Unit = dto.Unit;
        existing.Barcode = dto.Barcode;
        existing.IsActive = dto.IsActive;
        existing.AllowNegativeStock = dto.AllowNegativeStock;
        existing.UpdatedAt = _businessDayService.GetNowPakistanTime();

        // Note: Direct stock modifications must go through InventoryService.AdjustStockAsync
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task SoftDeleteProductAsync(int id)
    {
        var existing = await _db.Products.FindAsync(id)
            ?? throw new InvalidOperationException("Product not found.");
        existing.IsDeleted = true;
        existing.IsActive = false;
        existing.UpdatedAt = _businessDayService.GetNowPakistanTime();
        await _db.SaveChangesAsync();
    }
}
