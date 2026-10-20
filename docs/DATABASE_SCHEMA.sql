-- ============================================================================
-- SOFT2027 FMCG Wholesale & Distribution Management System
-- Production SQLite DDL Schema Specification
-- 100% Offline-First Architecture
-- ============================================================================

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;

-- ----------------------------------------------------------------------------
-- 1. SECURITY & SYSTEM AUDIT
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AdminUsers (
    AdminId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    FullName TEXT NOT NULL,
    Role TEXT NOT NULL CHECK(Role IN ('Administrator', 'Employee')) DEFAULT 'Administrator',
    Status TEXT NOT NULL CHECK(Status IN ('Active', 'Inactive')) DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastLogin DATETIME
);

CREATE TABLE IF NOT EXISTS AuditLogs (
    AuditId INTEGER PRIMARY KEY AUTOINCREMENT,
    AdminId INTEGER NOT NULL,
    Action TEXT NOT NULL,           -- 'CREATE', 'UPDATE', 'DELETE', 'PRICE_CHANGE', 'BACKUP', 'RESTORE'
    ModuleName TEXT NOT NULL,       -- 'Products', 'Sales', 'Purchases', 'PricingMemory', 'Ledger'
    RecordReference TEXT NOT NULL,  -- e.g., 'INV-2026-00142' or 'ProductID: 15'
    PreviousValue TEXT,
    NewValue TEXT,
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_AuditLogs_Module ON AuditLogs(ModuleName, Timestamp DESC);

-- ----------------------------------------------------------------------------
-- 2. MASTER DATA
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryName TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Description TEXT
);

CREATE TABLE IF NOT EXISTS Brands (
    BrandId INTEGER PRIMARY KEY AUTOINCREMENT,
    BrandName TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE IF NOT EXISTS Units (
    UnitId INTEGER PRIMARY KEY AUTOINCREMENT,
    UnitName TEXT NOT NULL UNIQUE COLLATE NOCASE -- e.g., 'Carton', 'Pack', 'Piece', 'Dozen'
);

CREATE TABLE IF NOT EXISTS Products (
    ProductId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductCode TEXT NOT NULL UNIQUE COLLATE NOCASE,
    Barcode TEXT UNIQUE,
    ProductName TEXT NOT NULL COLLATE NOCASE,
    CategoryId INTEGER,
    BrandId INTEGER,
    UnitId INTEGER,
    BasePurchasePrice REAL NOT NULL CHECK(BasePurchasePrice >= 0),
    PurchasePercentage REAL NOT NULL DEFAULT 0.0,
    DefaultSalePrice REAL NOT NULL CHECK(DefaultSalePrice >= 0),
    SellingPercentage REAL NOT NULL DEFAULT 0.0,
    CurrentStock REAL NOT NULL DEFAULT 0.0,
    MinimumStock REAL NOT NULL DEFAULT 0.0,
    Status TEXT NOT NULL CHECK(Status IN ('Active', 'Inactive')) DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId) ON DELETE SET NULL,
    FOREIGN KEY (BrandId) REFERENCES Brands(BrandId) ON DELETE SET NULL,
    FOREIGN KEY (UnitId) REFERENCES Units(UnitId) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS IDX_Products_Code ON Products(ProductCode);
CREATE INDEX IF NOT EXISTS IDX_Products_Name ON Products(ProductName);
CREATE INDEX IF NOT EXISTS IDX_Products_Stock ON Products(CurrentStock, MinimumStock);

CREATE TABLE IF NOT EXISTS Customers (
    CustomerId INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerCode TEXT NOT NULL UNIQUE COLLATE NOCASE,
    CustomerName TEXT NOT NULL COLLATE NOCASE,
    ContactPerson TEXT,
    Phone TEXT,
    Address TEXT,
    AreaTerritory TEXT,
    OpeningBalance REAL NOT NULL DEFAULT 0.0,
    OutstandingBalance REAL NOT NULL DEFAULT 0.0,
    CreditLimit REAL DEFAULT 0.0,
    Status TEXT NOT NULL CHECK(Status IN ('Active', 'Inactive')) DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS IDX_Customers_Code ON Customers(CustomerCode);
CREATE INDEX IF NOT EXISTS IDX_Customers_Name ON Customers(CustomerName);

CREATE TABLE IF NOT EXISTS Suppliers (
    SupplierId INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierCode TEXT NOT NULL UNIQUE COLLATE NOCASE,
    SupplierName TEXT NOT NULL COLLATE NOCASE,
    ContactPerson TEXT,
    Phone TEXT,
    Address TEXT,
    OpeningBalance REAL NOT NULL DEFAULT 0.0,
    OutstandingBalance REAL NOT NULL DEFAULT 0.0,
    Status TEXT NOT NULL CHECK(Status IN ('Active', 'Inactive')) DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS IDX_Suppliers_Code ON Suppliers(SupplierCode);

-- ----------------------------------------------------------------------------
-- 3. THREE-TIER PRICING MEMORY ENGINE TABLES
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CustomerProductPricingHistory (
    HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    SalePrice REAL NOT NULL CHECK(SalePrice >= 0),
    SellingPercentage REAL NOT NULL DEFAULT 0.0,
    DiscountPercentage REAL NOT NULL DEFAULT 0.0,
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    InvoiceNumber TEXT NOT NULL,
    TransactionDate DATETIME NOT NULL,
    AdminId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_CustPricing_Lookup ON CustomerProductPricingHistory(CustomerId, ProductId, TransactionDate DESC);
CREATE INDEX IF NOT EXISTS IDX_CustPricing_Product ON CustomerProductPricingHistory(ProductId, TransactionDate DESC);

CREATE TABLE IF NOT EXISTS SupplierProductPricingHistory (
    HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    PurchasePrice REAL NOT NULL CHECK(PurchasePrice >= 0),
    PurchasePercentage REAL NOT NULL DEFAULT 0.0,
    DiscountPercentage REAL NOT NULL DEFAULT 0.0,
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    InvoiceNumber TEXT NOT NULL,
    TransactionDate DATETIME NOT NULL,
    AdminId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE RESTRICT,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_SuppPricing_Lookup ON SupplierProductPricingHistory(SupplierId, ProductId, TransactionDate DESC);

-- ----------------------------------------------------------------------------
-- 4. SALES TRANSACTIONS (IMMUTABLE SNAPSHOTS)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Sales (
    SaleId INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNumber TEXT NOT NULL UNIQUE COLLATE NOCASE,
    CustomerId INTEGER NOT NULL,
    SaleDate DATETIME NOT NULL,
    SubTotal REAL NOT NULL CHECK(SubTotal >= 0),
    DiscountAmount REAL NOT NULL DEFAULT 0.0,
    NetTotal REAL NOT NULL CHECK(NetTotal >= 0),
    PaidAmount REAL NOT NULL DEFAULT 0.0,
    RemainingBalance REAL NOT NULL DEFAULT 0.0,
    PaymentType TEXT NOT NULL CHECK(PaymentType IN ('Cash', 'Credit', 'Split')) DEFAULT 'Credit',
    Status TEXT NOT NULL CHECK(Status IN ('Completed', 'Returned', 'Cancelled')) DEFAULT 'Completed',
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_Sales_Customer ON Sales(CustomerId, SaleDate DESC);
CREATE INDEX IF NOT EXISTS IDX_Sales_Invoice ON Sales(InvoiceNumber);
CREATE INDEX IF NOT EXISTS IDX_Sales_SaleDate ON Sales(SaleDate, Status);

CREATE TABLE IF NOT EXISTS SaleItems (
    SaleItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    ProductCodeAtSale TEXT NOT NULL DEFAULT '',
    ProductNameAtSale TEXT NOT NULL DEFAULT '',
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    -- Historical Immutability Columns:
    HistoricalSalePrice REAL NOT NULL CHECK(HistoricalSalePrice >= 0),
    HistoricalCostPrice REAL NOT NULL CHECK(HistoricalCostPrice >= 0),
    SellingPercentage REAL NOT NULL DEFAULT 0.0,
    DiscountPercentage REAL NOT NULL DEFAULT 0.0,
    LineTotal REAL NOT NULL CHECK(LineTotal >= 0),
    FOREIGN KEY (SaleId) REFERENCES Sales(SaleId) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_SaleItems_SaleId ON SaleItems(SaleId);
CREATE INDEX IF NOT EXISTS IDX_SaleItems_Product ON SaleItems(ProductId);

-- ----------------------------------------------------------------------------
-- 5. PURCHASE TRANSACTIONS
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Purchases (
    PurchaseId INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNumber TEXT NOT NULL UNIQUE COLLATE NOCASE,
    SupplierId INTEGER NOT NULL,
    PurchaseDate DATETIME NOT NULL,
    SubTotal REAL NOT NULL CHECK(SubTotal >= 0),
    DiscountAmount REAL NOT NULL DEFAULT 0.0,
    NetTotal REAL NOT NULL CHECK(NetTotal >= 0),
    PaidAmount REAL NOT NULL DEFAULT 0.0,
    RemainingBalance REAL NOT NULL DEFAULT 0.0,
    Status TEXT NOT NULL CHECK(Status IN ('Received', 'Returned', 'Cancelled')) DEFAULT 'Received',
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_Purchases_Supplier ON Purchases(SupplierId, PurchaseDate DESC);

CREATE TABLE IF NOT EXISTS PurchaseItems (
    PurchaseItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    ProductCodeAtPurchase TEXT NOT NULL DEFAULT '',
    ProductNameAtPurchase TEXT NOT NULL DEFAULT '',
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    HistoricalPurchasePrice REAL NOT NULL CHECK(HistoricalPurchasePrice >= 0),
    PurchasePercentage REAL NOT NULL DEFAULT 0.0,
    DiscountPercentage REAL NOT NULL DEFAULT 0.0,
    LineTotal REAL NOT NULL CHECK(LineTotal >= 0),
    FOREIGN KEY (PurchaseId) REFERENCES Purchases(PurchaseId) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_PurchaseItems_PurchaseId ON PurchaseItems(PurchaseId);

-- ----------------------------------------------------------------------------
-- 6. RETURNS MANAGEMENT
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SalesReturns (
    ReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnNumber TEXT NOT NULL UNIQUE,
    OriginalSaleId INTEGER,
    CustomerId INTEGER NOT NULL,
    ReturnDate DATETIME NOT NULL,
    TotalRefundAmount REAL NOT NULL,
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    FOREIGN KEY (OriginalSaleId) REFERENCES Sales(SaleId) ON DELETE SET NULL,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS SalesReturnItems (
    ReturnItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    RefundRate REAL NOT NULL,
    LineTotal REAL NOT NULL,
    FOREIGN KEY (ReturnId) REFERENCES SalesReturns(ReturnId) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS PurchaseReturns (
    ReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnNumber TEXT NOT NULL UNIQUE,
    OriginalPurchaseId INTEGER,
    SupplierId INTEGER NOT NULL,
    ReturnDate DATETIME NOT NULL,
    TotalRefundAmount REAL NOT NULL CHECK(TotalRefundAmount >= 0),
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    FOREIGN KEY (OriginalPurchaseId) REFERENCES Purchases(PurchaseId) ON DELETE SET NULL,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS PurchaseReturnItems (
    ReturnItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Quantity REAL NOT NULL CHECK(Quantity > 0),
    RefundRate REAL NOT NULL CHECK(RefundRate >= 0),
    LineTotal REAL NOT NULL CHECK(LineTotal >= 0),
    FOREIGN KEY (ReturnId) REFERENCES PurchaseReturns(ReturnId) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- 7. INVENTORY LEDGER & STOCK MOVEMENTS
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS StockMovements (
    MovementId INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL,
    MovementDate DATETIME NOT NULL,
    MovementType TEXT NOT NULL CHECK(MovementType IN ('OPENING', 'PURCHASE', 'SALE', 'SALE_RETURN', 'PURCHASE_RETURN', 'ADJUSTMENT')),
    ReferenceNumber TEXT NOT NULL,
    QuantityIn REAL NOT NULL DEFAULT 0.0,
    QuantityOut REAL NOT NULL DEFAULT 0.0,
    BalanceQuantity REAL NOT NULL,
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_Stock_Product_Date ON StockMovements(ProductId, MovementDate DESC);

-- ----------------------------------------------------------------------------
-- 8. FINANCIAL LEDGERS & ACCOUNTS
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CustomerLedger (
    LedgerId INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerId INTEGER NOT NULL,
    TransactionDate DATETIME NOT NULL,
    TransactionType TEXT NOT NULL, -- 'OPENING', 'SALE', 'RECEIPT', 'SALE_RETURN', 'ADJUSTMENT'
    ReferenceNumber TEXT NOT NULL,
    Description TEXT NOT NULL,
    Debit REAL NOT NULL DEFAULT 0.0,   -- Increases customer receivable
    Credit REAL NOT NULL DEFAULT 0.0,  -- Decreases customer receivable
    RunningBalance REAL NOT NULL,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_CustLedger_Customer_Date ON CustomerLedger(CustomerId, TransactionDate ASC);

CREATE TABLE IF NOT EXISTS SupplierLedger (
    LedgerId INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierId INTEGER NOT NULL,
    TransactionDate DATETIME NOT NULL,
    TransactionType TEXT NOT NULL, -- 'OPENING', 'PURCHASE', 'PAYMENT', 'PURCHASE_RETURN', 'ADJUSTMENT'
    ReferenceNumber TEXT NOT NULL,
    Description TEXT NOT NULL,
    Debit REAL NOT NULL DEFAULT 0.0,   -- Decreases supplier payable
    Credit REAL NOT NULL DEFAULT 0.0,  -- Increases supplier payable
    RunningBalance REAL NOT NULL,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_SuppLedger_Supplier_Date ON SupplierLedger(SupplierId, TransactionDate ASC);

CREATE TABLE IF NOT EXISTS Receipts (
    ReceiptId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptNumber TEXT NOT NULL UNIQUE,
    CustomerId INTEGER NOT NULL,
    ReceiptDate DATETIME NOT NULL,
    Amount REAL NOT NULL CHECK(Amount > 0),
    PaymentMode TEXT NOT NULL CHECK(PaymentMode IN ('Cash', 'Cheque', 'OnlineTransfer')),
    ReferenceDetails TEXT,
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Payments (
    PaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
    PaymentNumber TEXT NOT NULL UNIQUE,
    SupplierId INTEGER NOT NULL,
    PaymentDate DATETIME NOT NULL,
    Amount REAL NOT NULL CHECK(Amount > 0),
    PaymentMode TEXT NOT NULL CHECK(PaymentMode IN ('Cash', 'Cheque', 'OnlineTransfer')),
    ReferenceDetails TEXT,
    AdminId INTEGER NOT NULL,
    Remarks TEXT,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE RESTRICT,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Expenses (
    ExpenseId INTEGER PRIMARY KEY AUTOINCREMENT,
    ExpenseDate DATETIME NOT NULL,
    ExpenseCategory TEXT NOT NULL,
    Amount REAL NOT NULL CHECK(Amount > 0),
    Description TEXT NOT NULL,
    PaymentSource TEXT NOT NULL CHECK(PaymentSource IN ('Cash', 'Bank')),
    AdminId INTEGER NOT NULL,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_Expenses_Date ON Expenses(ExpenseDate);

-- ----------------------------------------------------------------------------
-- 9. CASH & BANK TRANSACTIONS
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CashTransactions (
    CashTransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionDate DATETIME NOT NULL,
    TransactionType TEXT NOT NULL CHECK(TransactionType IN ('OPENING', 'RECEIPT', 'PAYMENT', 'EXPENSE', 'SALE_CASH', 'PURCHASE_CASH', 'DEPOSIT', 'WITHDRAWAL', 'ADJUSTMENT')),
    ReferenceNumber TEXT NOT NULL,
    Description TEXT NOT NULL,
    Debit REAL NOT NULL DEFAULT 0.0,   -- Increases cash balance
    Credit REAL NOT NULL DEFAULT 0.0,  -- Decreases cash balance
    RunningBalance REAL NOT NULL,
    AdminId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_CashTx_Date ON CashTransactions(TransactionDate ASC);

CREATE TABLE IF NOT EXISTS BankTransactions (
    BankTransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
    TransactionDate DATETIME NOT NULL,
    TransactionType TEXT NOT NULL CHECK(TransactionType IN ('OPENING', 'RECEIPT', 'PAYMENT', 'EXPENSE', 'DEPOSIT', 'WITHDRAWAL', 'ADJUSTMENT')),
    ReferenceNumber TEXT NOT NULL,
    Description TEXT NOT NULL,
    Debit REAL NOT NULL DEFAULT 0.0,   -- Increases bank balance
    Credit REAL NOT NULL DEFAULT 0.0,  -- Decreases bank balance
    RunningBalance REAL NOT NULL,
    AdminId INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AdminId) REFERENCES AdminUsers(AdminId) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IDX_BankTx_Date ON BankTransactions(TransactionDate ASC);

CREATE TABLE IF NOT EXISTS SystemSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT NOT NULL,
    Description TEXT
);
