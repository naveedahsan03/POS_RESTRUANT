/* ============================================================================
   RESTAURANT POS & MANAGEMENT SYSTEM - SQL SERVER SCHEMA (3-TIER ARCHITECTURE)
   Timezone: Pakistan Standard Time (Asia/Karachi, UTC+5)
   Business Day Window: 6:00 PM (BusinessDate) to 4:00 AM (Next Day, Exclusive)
   ============================================================================ */

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

CREATE TABLE [SalePayments] (
    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [SaleId] INT NOT NULL,
    [PaymentMethod] INT NOT NULL,
    [Amount] DECIMAL(18,2) NOT NULL,
    [ReferenceNumber] NVARCHAR(100) NULL,
    [PaidAt] DATETIME2 NOT NULL,
    CONSTRAINT [FK_SalePayments_Sales] FOREIGN KEY ([SaleId]) REFERENCES [Sales]([Id]) ON DELETE CASCADE
);

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

CREATE TABLE [ExpenseCategories] (
    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(250) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT DATEADD(HOUR, 5, SYSUTCDATETIME()),
    [UpdatedAt] DATETIME2 NULL,
    [IsDeleted] BIT NOT NULL DEFAULT 0
);

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
