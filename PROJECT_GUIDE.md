# Restaurant POS & Management System — Complete Project Guide (`.md`)

## 1. Login Credentials (Default Accounts)

System run hone par ye accounts automatically create ho jate hain ([DbInitializer.cs](file:///d:/Data/Naveed/AI/Resturant/src/RestaurantPOS.DAL/Data/DbInitializer.cs)):

| Role | Email / Username | Password | Permissions |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@restaurant.pk` | `Admin@123` | Full System Access (POS, Products, Purchases, Expenses, Stock Ledger, Daily Reports, Cash Register, Users) |
| **Cashier** | `cashier@restaurant.pk` | `Cashier@123` | POS Screen, Sales Invoices, Print Receipts, Sales Return, Cash Register |

---

## 2. Technology Stack & 3-Tier Architecture

- **Backend**: C# ASP.NET Core 8 MVC (`net8.0`)
- **Database**: Microsoft SQL Server
- **ORM**: Entity Framework Core 8 (Code-First + Standalone SQL Script)
- **Authentication**: ASP.NET Core Identity (Role-Based: `Admin` & `Cashier`)
- **Frontend**: Bootstrap 5, Bootstrap Icons, JavaScript, jQuery
- **Reporting**: Printable Thermal Receipts, Printable Daily Business Report (PDF), and ClosedXML Excel (`.xlsx`) Export

### 3-Tier Folder Structure (`d:\Data\Naveed\AI\Resturant`)
```text
RestaurantPOS.sln
├── SQL/
│   └── 01_FullDatabaseSchema.sql          # SQL Server 22 Tables Script
└── src/
    ├── RestaurantPOS.DAL/                 # Tier 1: Data Access Layer (Entities, Enums, DbContext, Seed Data)
    ├── RestaurantPOS.BLL/                 # Tier 2: Business Logic Layer (DTOs, Interfaces, 9 Core Services)
    └── RestaurantPOS.Web/                 # Tier 3: Presentation Layer (Controllers, Razor Views, POS UI)
```

---

## 3. Restaurant Business Day Timing Rule (6:00 PM to Next Day 4:00 AM)

Centralized service: [BusinessDayService.cs](file:///d:/Data/Naveed/AI/Resturant/src/RestaurantPOS.BLL/Services/BusinessDayService.cs)

- **Time Zone**: Pakistan Standard Time (`Asia/Karachi`, UTC+5)
- **Business Day Start**: `6:00 PM` (`18:00:00`)
- **Business Day End**: Next Calendar Day `4:00 AM` (`04:00:00` exclusive)
- **Formula**:
  - Agar transaction ka waqt raat **12:00 AM se subah 3:59:59 AM** ke darmiyan ho, to `BusinessDate = Previous Calendar Date` (Pichli tareekh).
  - Agar transaction ka waqt **subah 4:00 AM ya uske baad** (sham 6:00 PM se raat 11:59:59 PM samait) ho, to `BusinessDate = Current Calendar Date`.

### Examples (Business Date: `17 November 2026`)
| Transaction Date & Time (PKT) | Assigned `BusinessDate` | Status |
| :--- | :--- | :--- |
| `17 Nov 2026, 06:15 PM` | `17 Nov 2026` | Included in 17 Nov Report |
| `17 Nov 2026, 11:50 PM` | `17 Nov 2026` | Included in 17 Nov Report |
| `18 Nov 2026, 12:30 AM` | `17 Nov 2026` | Included in 17 Nov Report (After Midnight) |
| `18 Nov 2026, 03:30 AM` | `17 Nov 2026` | Included in 17 Nov Report (After Midnight) |
| `18 Nov 2026, 04:00 AM` | `18 Nov 2026` | Next Business Day (`18 Nov 2026`) |

---

## 4. Modules & Phases Summary

1. **Phase 1 — Database & Authentication**:
   - 22 SQL Server tables ([01_FullDatabaseSchema.sql](file:///d:/Data/Naveed/AI/Resturant/SQL/01_FullDatabaseSchema.sql)), ASP.NET Core Identity login, Role-based access (`Admin`, `Cashier`).
2. **Phase 2 — Product & Category Management**:
   - Add/Edit/Soft-Delete products, categories, barcode search, low-stock alerts, opening stock ledger entry.
3. **Phase 3 — Touchscreen POS & Sales**:
   - Category buttons, search, cart +/- controls, Discount, Tax, **Cash / Card / Online / Split Payment**, **Hold & Resume Order**, Dine-In Table Management (`T-01` to `T-10`), Takeaway, Delivery, and 80mm Thermal Receipt Print.
4. **Phase 4 — Purchase & Purchase Return**:
   - Supplier management, Purchase Invoices (auto stock increase + supplier balance update), and Purchase Returns (prevents returning more than purchased).
5. **Phase 5 — Sales Return**:
   - Search original sale by `InvoiceNumber`, return selected items (prevents returning more than sold), auto stock increase + cash refund entry.
6. **Phase 6 — Expense Management**:
   - Categories (`Electricity`, `Gas`, `Rent`, `Staff Salary`, `Grocery`, `Maintenance`, `Other Expenses`) + Daily Expense entry with automatic `BusinessDate` calculation.
7. **Phase 7 — Inventory & Stock Ledger**:
   - Complete `StockLedger` tracking `OpeningStock`, `Purchase`, `PurchaseReturn`, `Sale`, `SalesReturn`, `StockAdjustment`, and `Wastage`.
8. **Phase 8 — Dashboard & Daily Sales Report**:
   - 12 Financial Metrics (Gross Sales, Discounts, Sales Returns, Net Sales, Cash Received, Card Payments, Online Payments, Total Expenses, Purchases, Estimated COGS, Gross Profit, Estimated Net Profit) + Product-wise, Category-wise, Payment-wise, and Hourly (`6:00 PM` – `4:00 AM`) report + Excel Export.
9. **Phase 9 — Cash Register & Closing**:
   - Opening Cash, Cash Sales, Cash Expenses, Cash Refunds, Expected Closing Cash, Actual Closing Cash, Cash Difference, and manual Business Day close.

---

## 5. How to Run the Project

### Step 1: Database Connection
Check [appsettings.json](file:///d:/Data/Naveed/AI/Resturant/src/RestaurantPOS.Web/appsettings.json):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RestaurantPOS_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```
*(Agar aapke paas SQL Server Express ya full SQL Server hai to `Server=YOUR_SERVER_NAME;` set kar dein).*

### Step 2: Build & Run Command
Terminal / PowerShell mein project folder (`d:\Data\Naveed\AI\Resturant`) mein ye command chalayein:
```powershell
dotnet run --project src/RestaurantPOS.Web/RestaurantPOS.Web.csproj
```
Application start hote hi database, tables, seed products, aur login accounts automatically create ho jayenge.
