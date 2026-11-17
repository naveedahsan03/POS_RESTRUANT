# Restaurant POS & Management System (3-Tier Architecture — ASP.NET Core 8 MVC)

Complete project guide is also available in [PROJECT_GUIDE.md](file:///d:/Data/Naveed/AI/Resturant/PROJECT_GUIDE.md).

## 1. Default Login Credentials

- **Admin Account**:
  - **Email**: `admin@restaurant.pk`
  - **Password**: `Admin@123`
- **Cashier Account**:
  - **Email**: `cashier@restaurant.pk`
  - **Password**: `Cashier@123`

---

## 2. 3-Tier Architecture Overview

```text
RestaurantPOS.sln
├── src/RestaurantPOS.DAL          # Tier 1: Data Access Layer (Entities, Enums, ApplicationDbContext, DbInitializer)
├── src/RestaurantPOS.BLL          # Tier 2: Business Logic Layer (Services, Interfaces, DTOs, BusinessDayService)
├── src/RestaurantPOS.Web          # Tier 3: Presentation Layer (MVC Controllers, Razor Views, Touchscreen POS UI)
└── SQL/01_FullDatabaseSchema.sql  # Complete standalone T-SQL DDL script for SQL Server (All 22 Tables)
```

---

## 3. Restaurant Business Day & Shift Timing (`6:00 PM` to `Next Day 4:00 AM` PKT)

Implemented centrally in `src/RestaurantPOS.BLL/Services/BusinessDayService.cs`:
- **Time Zone**: Pakistan Standard Time (`Asia/Karachi` / `Pakistan Standard Time`, UTC+5).
- **Rule**:
  - If `TransactionTime.TimeOfDay` is between `12:00:00 AM` (`00:00:00`) and `03:59:59.999 AM`, `BusinessDate = TransactionTime.Date.AddDays(-1)` (Previous Calendar Date).
  - Otherwise (`04:00:00 AM` onwards, including the `6:00 PM` evening shift), `BusinessDate = TransactionTime.Date` (Current Calendar Date).

---

## 4. How to Run

```powershell
dotnet run --project src/RestaurantPOS.Web/RestaurantPOS.Web.csproj
```
