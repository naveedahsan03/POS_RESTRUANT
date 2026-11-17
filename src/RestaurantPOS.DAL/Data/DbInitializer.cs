using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPOS.DAL.Entities;
using RestaurantPOS.DAL.Enums;

namespace RestaurantPOS.DAL.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roles = ["Admin", "Cashier"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Admin User
        var adminEmail = "admin@restaurant.pk";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Restaurant Manager (Admin)",
                RoleDisplay = "Admin",
                EmailConfirmed = true,
                IsActive = true
            };
            await userManager.CreateAsync(adminUser, "Admin@123");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        // 3. Seed Cashier User
        var cashierEmail = "cashier@restaurant.pk";
        var cashierUser = await userManager.FindByEmailAsync(cashierEmail);
        if (cashierUser == null)
        {
            cashierUser = new ApplicationUser
            {
                UserName = cashierEmail,
                Email = cashierEmail,
                FullName = "Night Shift Cashier",
                RoleDisplay = "Cashier",
                EmailConfirmed = true,
                IsActive = true
            };
            await userManager.CreateAsync(cashierUser, "Cashier@123");
            await userManager.AddToRoleAsync(cashierUser, "Cashier");
        }

        // 4. Seed Categories & Products
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "BBQ & Grill", Description = "Charcoal grilled kebabs and tikkas", DisplayOrder = 1 },
                new() { Name = "Karahi & Handi", Description = "Freshly cooked mutton and chicken karahi", DisplayOrder = 2 },
                new() { Name = "Fast Food & Burgers", Description = "Zinger, beef burgers, rolls & fries", DisplayOrder = 3 },
                new() { Name = "Tandoor & Breads", Description = "Naan, Roti, Paratha", DisplayOrder = 4 },
                new() { Name = "Beverages & Drinks", Description = "Soft drinks, water, lassi, tea", DisplayOrder = 5 }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            var nowPkt = DateTime.UtcNow.AddHours(5);
            var businessDate = nowPkt.TimeOfDay < TimeSpan.FromHours(4) ? nowPkt.Date.AddDays(-1) : nowPkt.Date;

            var products = new List<Product>
            {
                new() { Name = "Chicken Tikka (Leg/Chest)", CategoryId = categories[0].Id, PurchasePrice = 240, SalePrice = 420, CurrentStock = 80, MinimumStock = 15, Unit = "Piece", Barcode = "BBQ-001" },
                new() { Name = "Beef Seekh Kabab (Plate - 4 Pcs)", CategoryId = categories[0].Id, PurchasePrice = 280, SalePrice = 520, CurrentStock = 60, MinimumStock = 10, Unit = "Plate", Barcode = "BBQ-002" },
                new() { Name = "Malai Boti Roll", CategoryId = categories[0].Id, PurchasePrice = 140, SalePrice = 280, CurrentStock = 75, MinimumStock = 15, Unit = "Piece", Barcode = "BBQ-003" },
                new() { Name = "Chicken Karahi (Full)", CategoryId = categories[1].Id, PurchasePrice = 950, SalePrice = 1750, CurrentStock = 35, MinimumStock = 8, Unit = "KG", Barcode = "KRH-001" },
                new() { Name = "Mutton Karahi (Full)", CategoryId = categories[1].Id, PurchasePrice = 1850, SalePrice = 3100, CurrentStock = 25, MinimumStock = 5, Unit = "KG", Barcode = "KRH-002" },
                new() { Name = "Crispy Zinger Burger", CategoryId = categories[2].Id, PurchasePrice = 210, SalePrice = 450, CurrentStock = 90, MinimumStock = 20, Unit = "Piece", Barcode = "FFB-001" },
                new() { Name = "Loaded Masala Fries", CategoryId = categories[2].Id, PurchasePrice = 110, SalePrice = 260, CurrentStock = 100, MinimumStock = 20, Unit = "Plate", Barcode = "FFB-002" },
                new() { Name = "Garlic Naan", CategoryId = categories[3].Id, PurchasePrice = 35, SalePrice = 80, CurrentStock = 200, MinimumStock = 30, Unit = "Piece", Barcode = "TND-001" },
                new() { Name = "Roghni Naan", CategoryId = categories[3].Id, PurchasePrice = 30, SalePrice = 70, CurrentStock = 250, MinimumStock = 40, Unit = "Piece", Barcode = "TND-002" },
                new() { Name = "Chilled Soft Drink (500ml)", CategoryId = categories[4].Id, PurchasePrice = 75, SalePrice = 120, CurrentStock = 150, MinimumStock = 24, Unit = "Piece", Barcode = "BEV-001" },
                new() { Name = "Mineral Water (1.5L)", CategoryId = categories[4].Id, PurchasePrice = 55, SalePrice = 100, CurrentStock = 120, MinimumStock = 24, Unit = "Piece", Barcode = "BEV-002" }
            };
            context.Products.AddRange(products);
            await context.SaveChangesAsync();

            // Record Opening Stock in StockLedger for every seeded product
            foreach (var prod in products)
            {
                context.StockLedger.Add(new StockLedger
                {
                    ProductId = prod.Id,
                    BusinessDate = businessDate,
                    TransactionDateTime = nowPkt,
                    MovementType = StockMovementType.OpeningStock,
                    ReferenceNumber = $"OPEN-{prod.Id:D4}",
                    ReferenceId = prod.Id,
                    OpeningStock = 0,
                    QuantityIn = prod.CurrentStock,
                    QuantityOut = 0,
                    ClosingStock = prod.CurrentStock,
                    UnitCost = prod.PurchasePrice,
                    Remarks = "Initial Opening Stock",
                    CreatedByUserId = adminUser.Id
                });
            }
            await context.SaveChangesAsync();
        }

        // 5. Seed Restaurant Tables
        if (!await context.RestaurantTables.AnyAsync())
        {
            for (int i = 1; i <= 10; i++)
            {
                context.RestaurantTables.Add(new RestaurantTable
                {
                    TableNumber = $"T-{i:D2}",
                    Capacity = i <= 6 ? 4 : 8,
                    Status = TableStatus.Available,
                    IsActive = true
                });
            }
            await context.SaveChangesAsync();
        }

        // 6. Seed Default Suppliers & Walk-in Customer
        if (!await context.Suppliers.AnyAsync())
        {
            context.Suppliers.AddRange(
                new Supplier { Name = "Al-Madina Meat & Poultry Suppliers", ContactPerson = "Haji Tariq", Phone = "0300-1112233", Address = "Sabzi Mandi, Karachi", CurrentBalance = 0 },
                new Supplier { Name = "Karachi Beverage & Grocery Distributors", ContactPerson = "Kamran Ahmed", Phone = "0321-4445566", Address = "SITE Area, Karachi", CurrentBalance = 0 }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.Customers.AnyAsync())
        {
            context.Customers.Add(new Customer
            {
                Name = "Walk-In Customer",
                Phone = "0000-0000000",
                Address = "Counter",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // 7. Seed Expense Categories
        if (!await context.ExpenseCategories.AnyAsync())
        {
            string[] expenseCatNames = ["Electricity", "Gas", "Rent", "Staff Salary", "Grocery", "Maintenance", "Other Expenses"];
            foreach (var name in expenseCatNames)
            {
                context.ExpenseCategories.Add(new ExpenseCategory { Name = name, Description = $"{name} operational expense" });
            }
            await context.SaveChangesAsync();
        }
    }
}
