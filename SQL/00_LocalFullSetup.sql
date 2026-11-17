/* ============================================================================
   RESTAURANT POS & MANAGEMENT SYSTEM - COMPLETE LOCAL DATABASE SETUP
   Includes:
     1. Database Creation (RestaurantPOS_DB)
     2. ASP.NET Core Identity Tables (Users, Roles, Claims, Logins, Tokens)
     3. All 21 POS Domain Tables & Indexes
   Safe to re-run (uses IF NOT EXISTS / IF OBJECT_ID IS NULL checks).
   Note: Default Admin/Cashier users & master data are auto-seeded when the
         web app starts via DbInitializer.SeedAsync().
   ============================================================================ */

IF DB_ID(N'RestaurantPOS_DB') IS NULL
BEGIN
    CREATE DATABASE [RestaurantPOS_DB];
END
GO

USE [RestaurantPOS_DB];
GO

/* ============================================================================
   PART 1: ASP.NET CORE IDENTITY TABLES
   ============================================================================ */

IF OBJECT_ID(N'[AspNetRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(256) NULL,
        [NormalizedName] NVARCHAR(256) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] NVARCHAR(450) NOT NULL,
        [FullName] NVARCHAR(120) NOT NULL,
        [RoleDisplay] NVARCHAR(50) NOT NULL DEFAULT N'Cashier',
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UserName] NVARCHAR(256) NULL,
        [NormalizedUserName] NVARCHAR(256) NULL,
        [Email] NVARCHAR(256) NULL,
        [NormalizedEmail] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0,
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
        [LockoutEnd] DATETIMEOFFSET NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 0,
        [AccessFailedCount] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
    CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[AspNetRoleClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END
GO

IF OBJECT_ID(N'[AspNetUserClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END
GO

IF OBJECT_ID(N'[AspNetUserLogins]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [ProviderKey] NVARCHAR(450) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END
GO

IF OBJECT_ID(N'[AspNetUserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] NVARCHAR(450) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END
GO

IF OBJECT_ID(N'[AspNetUserTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] NVARCHAR(450) NOT NULL,
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(450) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END
GO

/* ============================================================================
   PART 2: POS DOMAIN TABLES
   ============================================================================ */

IF OBJECT_ID(N'[BusinessDays]', N'U') IS NULL
BEGIN
    CREATE TABLE [BusinessDays] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [BusinessDate] DATE NOT NULL,
        [WindowStart] DATETIME2 NOT NULL,
        [WindowEnd] DATETIME2 NOT NULL,
        [IsClosed] BIT NOT NULL DEFAULT 0,
        [OpenedAt] DATETIME2 NULL,
        [ClosedAt] DATETIME2 NULL,
        [OpenedByUserId] NVARCHAR(450) NULL,
        [ClosedByUserId] NVARCHAR(450) NULL,
        [ClosingNotes] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX [IX_BusinessDays_BusinessDate] ON [BusinessDays]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[Categories]', N'U') IS NULL
BEGIN
    CREATE TABLE [Categories] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(250) NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'[Products]', N'U') IS NULL
BEGIN
    CREATE TABLE [Products] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [CategoryId] INT NOT NULL,
        [PurchasePrice] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [SalePrice] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CurrentStock] DECIMAL(18,3) NOT NULL DEFAULT 0,
        [MinimumStock] DECIMAL(18,3) NOT NULL DEFAULT 0,
        [Unit] NVARCHAR(30) NOT NULL DEFAULT 'Piece',
        [Barcode] NVARCHAR(80) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [AllowNegativeStock] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_Products_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [Categories]([Id])
    );
    CREATE INDEX [IX_Products_Name] ON [Products]([Name]);
    CREATE INDEX [IX_Products_Barcode] ON [Products]([Barcode]);
    CREATE INDEX [IX_Products_CategoryId] ON [Products]([CategoryId]);
END
GO

IF OBJECT_ID(N'[Suppliers]', N'U') IS NULL
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [ContactPerson] NVARCHAR(100) NULL,
        [Phone] NVARCHAR(30) NULL,
        [Address] NVARCHAR(250) NULL,
        [CurrentBalance] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'[Customers]', N'U') IS NULL
BEGIN
    CREATE TABLE [Customers] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [Phone] NVARCHAR(30) NULL,
        [Address] NVARCHAR(250) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'[RestaurantTables]', N'U') IS NULL
BEGIN
    CREATE TABLE [RestaurantTables] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [TableNumber] NVARCHAR(50) NOT NULL,
        [Capacity] INT NOT NULL DEFAULT 4,
        [Status] INT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'[Sales]', N'U') IS NULL
BEGIN
    CREATE TABLE [Sales] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [InvoiceNumber] NVARCHAR(40) NOT NULL,
        [ClientRequestToken] NVARCHAR(80) NULL,
        [BusinessDate] DATE NOT NULL,
        [SaleDateTime] DATETIME2 NOT NULL,
        [OrderType] INT NOT NULL DEFAULT 1,
        [RestaurantTableId] INT NULL,
        [CustomerId] INT NULL,
        [CustomerName] NVARCHAR(120) NULL,
        [CustomerPhone] NVARCHAR(30) NULL,
        [DeliveryAddress] NVARCHAR(250) NULL,
        [SubTotal] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [DiscountAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TaxPercentage] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TaxAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [PaymentMethod] INT NOT NULL DEFAULT 1,
        [ReceivedAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [ChangeAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [UserId] NVARCHAR(450) NOT NULL,
        [Status] INT NOT NULL DEFAULT 1,
        [Notes] NVARCHAR(300) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_Sales_RestaurantTables] FOREIGN KEY ([RestaurantTableId]) REFERENCES [RestaurantTables]([Id]),
        CONSTRAINT [FK_Sales_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [Customers]([Id])
    );
    CREATE UNIQUE INDEX [IX_Sales_InvoiceNumber] ON [Sales]([InvoiceNumber]);
    CREATE INDEX [IX_Sales_BusinessDate_Status] ON [Sales]([BusinessDate], [Status]);
    CREATE INDEX [IX_Sales_SaleDateTime] ON [Sales]([SaleDateTime]);
END
GO

IF OBJECT_ID(N'[SaleDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [SaleDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SaleId] INT NOT NULL,
        [ProductId] INT NOT NULL,
        [ProductName] NVARCHAR(150) NOT NULL,
        [Quantity] DECIMAL(18,3) NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [UnitCostSnapshot] DECIMAL(18,2) NOT NULL,
        [DiscountAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [LineTotal] DECIMAL(18,2) NOT NULL,
        CONSTRAINT [FK_SaleDetails_Sales] FOREIGN KEY ([SaleId]) REFERENCES [Sales]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SaleDetails_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
END
GO

IF OBJECT_ID(N'[SalePayments]', N'U') IS NULL
BEGIN
    CREATE TABLE [SalePayments] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SaleId] INT NOT NULL,
        [PaymentMethod] INT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [ReferenceNumber] NVARCHAR(100) NULL,
        [PaidAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_SalePayments_Sales] FOREIGN KEY ([SaleId]) REFERENCES [Sales]([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[SalesReturns]', N'U') IS NULL
BEGIN
    CREATE TABLE [SalesReturns] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ReturnNumber] NVARCHAR(40) NOT NULL,
        [SaleId] INT NOT NULL,
        [OriginalInvoiceNumber] NVARCHAR(40) NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [ReturnDateTime] DATETIME2 NOT NULL,
        [TotalRefundAmount] DECIMAL(18,2) NOT NULL,
        [RefundMethod] INT NOT NULL DEFAULT 1,
        [Reason] NVARCHAR(300) NOT NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_SalesReturns_Sales] FOREIGN KEY ([SaleId]) REFERENCES [Sales]([Id])
    );
    CREATE UNIQUE INDEX [IX_SalesReturns_ReturnNumber] ON [SalesReturns]([ReturnNumber]);
    CREATE INDEX [IX_SalesReturns_BusinessDate] ON [SalesReturns]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[SalesReturnDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [SalesReturnDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SalesReturnId] INT NOT NULL,
        [SaleDetailId] INT NOT NULL,
        [ProductId] INT NOT NULL,
        [ReturnQuantity] DECIMAL(18,3) NOT NULL,
        [UnitRefundPrice] DECIMAL(18,2) NOT NULL,
        [UnitCostSnapshot] DECIMAL(18,2) NOT NULL,
        [RefundLineTotal] DECIMAL(18,2) NOT NULL,
        CONSTRAINT [FK_SalesReturnDetails_SalesReturns] FOREIGN KEY ([SalesReturnId]) REFERENCES [SalesReturns]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SalesReturnDetails_SaleDetails] FOREIGN KEY ([SaleDetailId]) REFERENCES [SaleDetails]([Id]),
        CONSTRAINT [FK_SalesReturnDetails_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
END
GO

IF OBJECT_ID(N'[Purchases]', N'U') IS NULL
BEGIN
    CREATE TABLE [Purchases] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseInvoiceNumber] NVARCHAR(50) NOT NULL,
        [SupplierId] INT NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [PurchaseDate] DATETIME2 NOT NULL,
        [TotalAmount] DECIMAL(18,2) NOT NULL,
        [PaidAmount] DECIMAL(18,2) NOT NULL,
        [RemainingPayable] DECIMAL(18,2) NOT NULL,
        [PaymentStatus] INT NOT NULL DEFAULT 1,
        [PaymentMethod] INT NOT NULL DEFAULT 1,
        [Notes] NVARCHAR(300) NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_Purchases_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers]([Id])
    );
    CREATE UNIQUE INDEX [IX_Purchases_PurchaseInvoiceNumber] ON [Purchases]([PurchaseInvoiceNumber]);
    CREATE INDEX [IX_Purchases_BusinessDate] ON [Purchases]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[PurchaseDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseId] INT NOT NULL,
        [ProductId] INT NOT NULL,
        [Quantity] DECIMAL(18,3) NOT NULL,
        [PurchasePrice] DECIMAL(18,2) NOT NULL,
        [LineTotal] DECIMAL(18,2) NOT NULL,
        CONSTRAINT [FK_PurchaseDetails_Purchases] FOREIGN KEY ([PurchaseId]) REFERENCES [Purchases]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseDetails_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
END
GO

IF OBJECT_ID(N'[PurchaseReturns]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseReturns] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ReturnNumber] NVARCHAR(50) NOT NULL,
        [PurchaseId] INT NOT NULL,
        [SupplierId] INT NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [ReturnDate] DATETIME2 NOT NULL,
        [TotalReturnAmount] DECIMAL(18,2) NOT NULL,
        [Reason] NVARCHAR(300) NOT NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_PurchaseReturns_Purchases] FOREIGN KEY ([PurchaseId]) REFERENCES [Purchases]([Id]),
        CONSTRAINT [FK_PurchaseReturns_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers]([Id])
    );
END
GO

IF OBJECT_ID(N'[PurchaseReturnDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseReturnDetails] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseReturnId] INT NOT NULL,
        [PurchaseDetailId] INT NOT NULL,
        [ProductId] INT NOT NULL,
        [ReturnQuantity] DECIMAL(18,3) NOT NULL,
        [PurchasePrice] DECIMAL(18,2) NOT NULL,
        [ReturnLineTotal] DECIMAL(18,2) NOT NULL,
        CONSTRAINT [FK_PurchaseReturnDetails_PurchaseReturns] FOREIGN KEY ([PurchaseReturnId]) REFERENCES [PurchaseReturns]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseReturnDetails_PurchaseDetails] FOREIGN KEY ([PurchaseDetailId]) REFERENCES [PurchaseDetails]([Id]),
        CONSTRAINT [FK_PurchaseReturnDetails_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
END
GO

IF OBJECT_ID(N'[ExpenseCategories]', N'U') IS NULL
BEGIN
    CREATE TABLE [ExpenseCategories] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(250) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'[Expenses]', N'U') IS NULL
BEGIN
    CREATE TABLE [Expenses] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ExpenseCategoryId] INT NOT NULL,
        [Title] NVARCHAR(150) NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [ExpenseDateTime] DATETIME2 NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [PaymentMethod] INT NOT NULL DEFAULT 1,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_Expenses_ExpenseCategories] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [ExpenseCategories]([Id])
    );
    CREATE INDEX [IX_Expenses_BusinessDate] ON [Expenses]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[StockLedger]', N'U') IS NULL
BEGIN
    CREATE TABLE [StockLedger] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [TransactionDateTime] DATETIME2 NOT NULL,
        [MovementType] INT NOT NULL,
        [ReferenceNumber] NVARCHAR(50) NULL,
        [ReferenceId] INT NULL,
        [OpeningStock] DECIMAL(18,3) NOT NULL,
        [QuantityIn] DECIMAL(18,3) NOT NULL,
        [QuantityOut] DECIMAL(18,3) NOT NULL,
        [ClosingStock] DECIMAL(18,3) NOT NULL,
        [UnitCost] DECIMAL(18,2) NOT NULL,
        [Remarks] NVARCHAR(300) NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_StockLedger_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
    CREATE INDEX [IX_StockLedger_ProductId_TransactionDateTime] ON [StockLedger]([ProductId], [TransactionDateTime]);
    CREATE INDEX [IX_StockLedger_BusinessDate] ON [StockLedger]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[StockAdjustments]', N'U') IS NULL
BEGIN
    CREATE TABLE [StockAdjustments] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AdjustmentNumber] NVARCHAR(40) NOT NULL,
        [ProductId] INT NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [AdjustmentDateTime] DATETIME2 NOT NULL,
        [MovementType] INT NOT NULL DEFAULT 6,
        [PreviousStock] DECIMAL(18,3) NOT NULL,
        [AdjustedQuantity] DECIMAL(18,3) NOT NULL,
        [NewStock] DECIMAL(18,3) NOT NULL,
        [Reason] NVARCHAR(300) NOT NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_StockAdjustments_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id])
    );
END
GO

IF OBJECT_ID(N'[CashRegisters]', N'U') IS NULL
BEGIN
    CREATE TABLE [CashRegisters] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [BusinessDate] DATE NOT NULL,
        [OpenedAt] DATETIME2 NOT NULL,
        [ClosedAt] DATETIME2 NULL,
        [OpeningCash] DECIMAL(18,2) NOT NULL,
        [CashSales] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CashExpenses] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CashRefunds] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CashReceived] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [ExpectedClosingCash] DECIMAL(18,2) NOT NULL,
        [ActualClosingCash] DECIMAL(18,2) NULL,
        [CashDifference] DECIMAL(18,2) NULL,
        [IsClosed] BIT NOT NULL DEFAULT 0,
        [OpenedByUserId] NVARCHAR(450) NOT NULL,
        [ClosedByUserId] NVARCHAR(450) NULL,
        [ClosingNotes] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX [IX_CashRegisters_BusinessDate] ON [CashRegisters]([BusinessDate]);
END
GO

IF OBJECT_ID(N'[CashRegisterTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [CashRegisterTransactions] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CashRegisterId] INT NOT NULL,
        [BusinessDate] DATE NOT NULL,
        [TransactionDateTime] DATETIME2 NOT NULL,
        [TransactionType] INT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [ReferenceNumber] NVARCHAR(50) NULL,
        [Description] NVARCHAR(250) NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_CashRegisterTransactions_CashRegisters] FOREIGN KEY ([CashRegisterId]) REFERENCES [CashRegisters]([Id]) ON DELETE CASCADE
    );
END
GO
