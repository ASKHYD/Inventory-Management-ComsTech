using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public ReportRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<OperationalReport> GetOperationalReportAsync(OperationalReportRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.ReportKey)) throw new ArgumentException("Choose a report.", nameof(request));
            if (request.EndDate.Date < request.StartDate.Date) throw new ArgumentException("The end date must be on or after the start date.", nameof(request));

            var definition = GetOperationalReportDefinition(request.ReportKey);
            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var endExclusive = request.EndDate.Date.AddDays(1);
            var args = new
            {
                StartDate = request.StartDate.Date,
                EndExclusive = endExclusive,
                EndDate = request.EndDate.Date,
                CustomerId = request.CustomerId,
                SupplierId = request.SupplierId,
                ProductId = request.ProductId,
                CategoryId = request.CategoryId,
                ExpenseCategory = string.IsNullOrWhiteSpace(request.ExpenseCategory) ? null : request.ExpenseCategory.Trim()
            };
            var records = await connection.QueryAsync(new CommandDefinition(definition.Sql, args, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var result = new OperationalReport { Title = definition.Title, Columns = definition.Columns };
            foreach (var record in records)
            {
                var values = (IDictionary<string, object>)record;
                result.Rows.Add(new OperationalReportRow
                {
                    Values = values.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase)
                });
            }
            return result;
        }

        public async Task<IReadOnlyList<string>> GetExpenseCategoriesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var values = await connection.QueryAsync<string>(new CommandDefinition("SELECT DISTINCT ExpenseCategory FROM Expenses ORDER BY ExpenseCategory COLLATE NOCASE", cancellationToken: cancellationToken)).ConfigureAwait(false);
            return values.AsList();
        }

        private static ReportDefinition GetOperationalReportDefinition(string key)
        {
            const string salesFilter = "s.SaleDate >= @StartDate AND s.SaleDate < @EndExclusive AND s.Status <> 'Cancelled' AND (@CustomerId IS NULL OR s.CustomerId = @CustomerId)";
            const string purchaseFilter = "p.PurchaseDate >= @StartDate AND p.PurchaseDate < @EndExclusive AND p.Status <> 'Cancelled' AND (@SupplierId IS NULL OR p.SupplierId = @SupplierId)";
            const string productFilter = "(@ProductId IS NULL OR pr.ProductId = @ProductId) AND (@CategoryId IS NULL OR pr.CategoryId = @CategoryId)";
            const string customerFilter = "(@CustomerId IS NULL OR c.CustomerId = @CustomerId)";
            const string supplierFilter = "(@SupplierId IS NULL OR s.SupplierId = @SupplierId)";
            const string expenseFilter = "e.ExpenseDate >= @StartDate AND e.ExpenseDate < @EndExclusive AND (@ExpenseCategory IS NULL OR e.ExpenseCategory = @ExpenseCategory)";
            const string salesByProductExists = "(@ProductId IS NULL OR EXISTS (SELECT 1 FROM SaleItems fx WHERE fx.SaleId=s.SaleId AND fx.ProductId=@ProductId)) AND (@CategoryId IS NULL OR EXISTS (SELECT 1 FROM SaleItems fx JOIN Products fp ON fp.ProductId=fx.ProductId WHERE fx.SaleId=s.SaleId AND fp.CategoryId=@CategoryId))";
            const string purchasesByProductExists = "(@ProductId IS NULL OR EXISTS (SELECT 1 FROM PurchaseItems fx WHERE fx.PurchaseId=p.PurchaseId AND fx.ProductId=@ProductId)) AND (@CategoryId IS NULL OR EXISTS (SELECT 1 FROM PurchaseItems fx JOIN Products fp ON fp.ProductId=fx.ProductId WHERE fx.PurchaseId=p.PurchaseId AND fp.CategoryId=@CategoryId))";

            return key switch
            {
                "SalesByDate" or "SalesByInvoice" => Def("Sales by date range and invoice", [C("SaleDate", "Date", "d"), C("InvoiceNumber", "Invoice"), C("CustomerName", "Customer"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net sales", "N2"), C("PaidAmount", "Paid", "N2"), C("RemainingBalance", "Outstanding", "N2"), C("PaymentStatus", "Payment status")],
                    $"SELECT s.SaleDate,s.InvoiceNumber,c.CustomerName,s.SubTotal,s.DiscountAmount,s.NetTotal,s.PaidAmount,s.RemainingBalance, CASE WHEN s.RemainingBalance<=0 THEN 'Paid' WHEN s.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus FROM Sales s JOIN Customers c ON c.CustomerId=s.CustomerId WHERE {salesFilter} AND {salesByProductExists} ORDER BY s.SaleDate DESC,s.SaleId DESC"),
                "SalesByCustomer" => Def("Sales by customer", [C("CustomerCode", "Code"), C("CustomerName", "Customer"), C("InvoiceCount", "Invoices", "N0"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetSales", "Net sales", "N2"), C("Paid", "Paid", "N2"), C("Outstanding", "Outstanding", "N2")],
                    $"SELECT c.CustomerCode,c.CustomerName,COUNT(*) AS InvoiceCount,SUM(s.SubTotal) AS SubTotal,SUM(s.DiscountAmount) AS DiscountAmount,SUM(s.NetTotal) AS NetSales,SUM(s.PaidAmount) AS Paid,SUM(s.RemainingBalance) AS Outstanding FROM Sales s JOIN Customers c ON c.CustomerId=s.CustomerId WHERE {salesFilter} AND {salesByProductExists} GROUP BY c.CustomerId,c.CustomerCode,c.CustomerName ORDER BY NetSales DESC"),
                "SalesByProduct" => Def("Sales by product", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("Quantity", "Quantity", "N2"), C("GrossSales", "Net line sales", "N2"), C("Discount", "Discount", "N2")],
                    $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,SUM(si.Quantity) AS Quantity,SUM(si.LineTotal) AS GrossSales,SUM(si.Quantity*si.HistoricalSalePrice-si.LineTotal) AS Discount FROM SaleItems si JOIN Sales s ON s.SaleId=si.SaleId JOIN Products pr ON pr.ProductId=si.ProductId LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE {salesFilter} AND {productFilter} GROUP BY pr.ProductId,pr.ProductCode,pr.ProductName,cat.CategoryName ORDER BY GrossSales DESC"),
                "SalesReturns" => Def("Sales returns", [C("ReturnDate", "Date", "d"), C("ReturnNumber", "Return"), C("InvoiceNumber", "Original invoice"), C("CustomerName", "Customer"), C("ProductCode", "Code"), C("ProductName", "Product"), C("Quantity", "Returned qty", "N2"), C("RefundRate", "Refund rate", "N2"), C("LineTotal", "Refund", "N2")],
                    "SELECT r.ReturnDate,r.ReturnNumber,s.InvoiceNumber,c.CustomerName,pr.ProductCode,pr.ProductName,ri.Quantity,ri.RefundRate,ri.LineTotal FROM SalesReturns r JOIN Customers c ON c.CustomerId=r.CustomerId LEFT JOIN Sales s ON s.SaleId=r.OriginalSaleId JOIN SalesReturnItems ri ON ri.ReturnId=r.ReturnId JOIN Products pr ON pr.ProductId=ri.ProductId WHERE r.ReturnDate>=@StartDate AND r.ReturnDate<@EndExclusive AND (@CustomerId IS NULL OR r.CustomerId=@CustomerId) AND " + productFilter + " ORDER BY r.ReturnDate DESC,r.ReturnId DESC"),
                "SalesDiscounts" => Def("Sales discounts and net sales", [C("SaleDate", "Date", "d"), C("InvoiceNumber", "Invoice"), C("CustomerName", "Customer"), C("SubTotal", "Gross subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net sales", "N2")],
                    $"SELECT s.SaleDate,s.InvoiceNumber,c.CustomerName,s.SubTotal,s.DiscountAmount,s.NetTotal FROM Sales s JOIN Customers c ON c.CustomerId=s.CustomerId WHERE {salesFilter} AND {salesByProductExists} ORDER BY s.SaleDate DESC"),
                "SalesPaymentStatus" => Def("Sales by payment status", [C("PaymentStatus", "Payment status"), C("InvoiceCount", "Invoices", "N0"), C("NetSales", "Net sales", "N2"), C("Paid", "Paid", "N2"), C("Outstanding", "Outstanding", "N2")],
                    $"SELECT CASE WHEN s.RemainingBalance<=0 THEN 'Paid' WHEN s.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus,COUNT(*) AS InvoiceCount,SUM(s.NetTotal) AS NetSales,SUM(s.PaidAmount) AS Paid,SUM(s.RemainingBalance) AS Outstanding FROM Sales s WHERE {salesFilter} AND {salesByProductExists} GROUP BY PaymentStatus ORDER BY PaymentStatus"),
                "PurchasesByDate" or "PurchasesByInvoice" => Def("Purchases by date range and bill", [C("PurchaseDate", "Date", "d"), C("InvoiceNumber", "Bill"), C("SupplierName", "Supplier"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net purchases", "N2"), C("PaidAmount", "Paid", "N2"), C("RemainingBalance", "Outstanding", "N2"), C("PaymentStatus", "Payment status")],
                    $"SELECT p.PurchaseDate,p.InvoiceNumber,s.SupplierName,p.SubTotal,p.DiscountAmount,p.NetTotal,p.PaidAmount,p.RemainingBalance,CASE WHEN p.RemainingBalance<=0 THEN 'Paid' WHEN p.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus FROM Purchases p JOIN Suppliers s ON s.SupplierId=p.SupplierId WHERE {purchaseFilter} AND {purchasesByProductExists} ORDER BY p.PurchaseDate DESC,p.PurchaseId DESC"),
                "PurchasesBySupplier" => Def("Purchases by supplier", [C("SupplierCode", "Code"), C("SupplierName", "Supplier"), C("BillCount", "Bills", "N0"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetPurchases", "Net purchases", "N2"), C("Paid", "Paid", "N2"), C("Outstanding", "Outstanding", "N2")],
                    $"SELECT s.SupplierCode,s.SupplierName,COUNT(*) AS BillCount,SUM(p.SubTotal) AS SubTotal,SUM(p.DiscountAmount) AS DiscountAmount,SUM(p.NetTotal) AS NetPurchases,SUM(p.PaidAmount) AS Paid,SUM(p.RemainingBalance) AS Outstanding FROM Purchases p JOIN Suppliers s ON s.SupplierId=p.SupplierId WHERE {purchaseFilter} AND {purchasesByProductExists} GROUP BY s.SupplierId,s.SupplierCode,s.SupplierName ORDER BY NetPurchases DESC"),
                "PurchasesByProduct" => Def("Purchases by product", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("Quantity", "Quantity", "N2"), C("NetPurchases", "Net purchases", "N2"), C("Discount", "Discount", "N2")],
                    $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,SUM(pi.Quantity) AS Quantity,SUM(pi.LineTotal) AS NetPurchases,SUM(pi.Quantity*pi.HistoricalPurchasePrice-pi.LineTotal) AS Discount FROM PurchaseItems pi JOIN Purchases p ON p.PurchaseId=pi.PurchaseId JOIN Products pr ON pr.ProductId=pi.ProductId LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE {purchaseFilter} AND {productFilter} GROUP BY pr.ProductId,pr.ProductCode,pr.ProductName,cat.CategoryName ORDER BY NetPurchases DESC"),
                "PurchaseReturns" => Def("Purchase returns", [C("ReturnDate", "Date", "d"), C("ReturnNumber", "Return"), C("InvoiceNumber", "Original bill"), C("SupplierName", "Supplier"), C("ProductCode", "Code"), C("ProductName", "Product"), C("Quantity", "Returned qty", "N2"), C("RefundRate", "Return rate", "N2"), C("LineTotal", "Return value", "N2")],
                    "SELECT r.ReturnDate,r.ReturnNumber,p.InvoiceNumber,s.SupplierName,pr.ProductCode,pr.ProductName,ri.Quantity,ri.RefundRate,ri.LineTotal FROM PurchaseReturns r JOIN Suppliers s ON s.SupplierId=r.SupplierId LEFT JOIN Purchases p ON p.PurchaseId=r.OriginalPurchaseId JOIN PurchaseReturnItems ri ON ri.ReturnId=r.ReturnId JOIN Products pr ON pr.ProductId=ri.ProductId WHERE r.ReturnDate>=@StartDate AND r.ReturnDate<@EndExclusive AND (@SupplierId IS NULL OR r.SupplierId=@SupplierId) AND " + productFilter + " ORDER BY r.ReturnDate DESC,r.ReturnId DESC"),
                "PurchaseDiscounts" => Def("Purchase discounts", [C("PurchaseDate", "Date", "d"), C("InvoiceNumber", "Bill"), C("SupplierName", "Supplier"), C("SubTotal", "Gross subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net purchases", "N2")],
                    $"SELECT p.PurchaseDate,p.InvoiceNumber,s.SupplierName,p.SubTotal,p.DiscountAmount,p.NetTotal FROM Purchases p JOIN Suppliers s ON s.SupplierId=p.SupplierId WHERE {purchaseFilter} AND {purchasesByProductExists} ORDER BY p.PurchaseDate DESC"),
                "PurchasesPaymentStatus" => Def("Purchases by payment status", [C("PaymentStatus", "Payment status"), C("BillCount", "Bills", "N0"), C("NetPurchases", "Net purchases", "N2"), C("Paid", "Paid", "N2"), C("Outstanding", "Outstanding", "N2")],
                    $"SELECT CASE WHEN p.RemainingBalance<=0 THEN 'Paid' WHEN p.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus,COUNT(*) AS BillCount,SUM(p.NetTotal) AS NetPurchases,SUM(p.PaidAmount) AS Paid,SUM(p.RemainingBalance) AS Outstanding FROM Purchases p WHERE {purchaseFilter} AND {purchasesByProductExists} GROUP BY PaymentStatus ORDER BY PaymentStatus"),
                "CurrentStock" or "StockValuation" => Def(key == "CurrentStock" ? "Current stock" : "Stock valuation at purchase cost", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("CurrentStock", "On hand", "N2"), C("MinimumStock", "Minimum", "N2"), C("UnitCost", "Unit cost", "N2"), C("StockValue", "Stock value", "N2"), C("DefaultSalePrice", "Default sale price", "N2"), C("RetailValue", "Potential retail value", "N2"), C("LowStock", "Stock status")],
                    $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,pr.CurrentStock,pr.MinimumStock,pr.BasePurchasePrice AS UnitCost,pr.CurrentStock*pr.BasePurchasePrice AS StockValue,pr.DefaultSalePrice,pr.CurrentStock*pr.DefaultSalePrice AS RetailValue,CASE WHEN pr.CurrentStock<=pr.MinimumStock THEN 'Low stock' ELSE '' END AS LowStock FROM Products pr LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE pr.Status='Active' AND {productFilter} ORDER BY pr.ProductName"),
                "LowStock" => Def("Low-stock items", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("CurrentStock", "On hand", "N2"), C("MinimumStock", "Minimum", "N2")],
                    $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,pr.CurrentStock,pr.MinimumStock FROM Products pr LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE pr.Status='Active' AND pr.CurrentStock<=pr.MinimumStock AND {productFilter} ORDER BY pr.ProductName"),
                "StockMovements" or "StockMovementByProduct" => Def(key == "StockMovements" ? "Stock movement history" : "Product-wise stock movement", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("MovementDate", "Date", "d"), C("MovementType", "Type"), C("ReferenceNumber", "Reference"), C("QuantityIn", "In", "N2"), C("QuantityOut", "Out", "N2"), C("BalanceQuantity", "Balance", "N2"), C("Remarks", "Reason")],
                    key == "StockMovements"
                        ? $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,m.MovementDate,m.MovementType,m.ReferenceNumber,m.QuantityIn,m.QuantityOut,m.BalanceQuantity,m.Remarks FROM StockMovements m JOIN Products pr ON pr.ProductId=m.ProductId LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE m.MovementDate>=@StartDate AND m.MovementDate<@EndExclusive AND {productFilter} ORDER BY m.MovementDate DESC,m.MovementId DESC"
                        : $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,NULL AS MovementDate,'PERIOD TOTAL' AS MovementType,'' AS ReferenceNumber,SUM(m.QuantityIn) AS QuantityIn,SUM(m.QuantityOut) AS QuantityOut,SUM(m.QuantityIn-m.QuantityOut) AS BalanceQuantity,'' AS Remarks FROM StockMovements m JOIN Products pr ON pr.ProductId=m.ProductId LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE m.MovementDate>=@StartDate AND m.MovementDate<@EndExclusive AND {productFilter} GROUP BY pr.ProductId,pr.ProductCode,pr.ProductName,cat.CategoryName ORDER BY pr.ProductName"),
                "StockAdjustments" => Def("Stock adjustments", [C("MovementDate", "Date", "d"), C("ProductCode", "Code"), C("ProductName", "Product"), C("ReferenceNumber", "Reference"), C("QuantityIn", "Added", "N2"), C("QuantityOut", "Removed", "N2"), C("BalanceQuantity", "Balance", "N2"), C("Remarks", "Reason")],
                    $"SELECT m.MovementDate,pr.ProductCode,pr.ProductName,m.ReferenceNumber,m.QuantityIn,m.QuantityOut,m.BalanceQuantity,m.Remarks FROM StockMovements m JOIN Products pr ON pr.ProductId=m.ProductId WHERE m.MovementType='ADJUSTMENT' AND m.MovementDate>=@StartDate AND m.MovementDate<@EndExclusive AND {productFilter} ORDER BY m.MovementDate DESC"),
                "Customers" or "OutstandingReceivables" => Def(key == "Customers" ? "Customer list and balances" : "Outstanding receivables", [C("CustomerCode", "Code"), C("CustomerName", "Customer"), C("Phone", "Phone"), C("OutstandingBalance", "Receivable", "N2"), C("CreditLimit", "Credit limit", "N2"), C("Status", "Status")],
                    $"SELECT c.CustomerCode,c.CustomerName,c.Phone,c.OutstandingBalance,c.CreditLimit,c.Status FROM Customers c WHERE {customerFilter} AND ('{key}'<>'OutstandingReceivables' OR c.OutstandingBalance>0) ORDER BY c.CustomerName"),
                "CustomerSalesHistory" => Def("Customer sales history", [C("SaleDate", "Date", "d"), C("InvoiceNumber", "Invoice"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net sales", "N2"), C("PaidAmount", "Paid", "N2"), C("RemainingBalance", "Outstanding", "N2"), C("PaymentStatus", "Status")],
                    $"SELECT s.SaleDate,s.InvoiceNumber,s.SubTotal,s.DiscountAmount,s.NetTotal,s.PaidAmount,s.RemainingBalance,CASE WHEN s.RemainingBalance<=0 THEN 'Paid' WHEN s.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus FROM Sales s WHERE {salesFilter} AND {salesByProductExists} ORDER BY s.SaleDate DESC"),
                "CustomerPriceHistory" or "CustomerPriceChanges" => Def(key == "CustomerPriceHistory" ? "Customer/product selling-price history" : "Customer/product price changes and discounts", [C("TransactionDate", "Date", "d"), C("CustomerName", "Customer"), C("ProductCode", "Code"), C("ProductName", "Product"), C("PreviousPrice", "Previous rate", "N2"), C("SalePrice", "New rate", "N2"), C("DiscountPercentage", "Discount %", "N2"), C("Quantity", "Quantity", "N2"), C("InvoiceNumber", "Invoice")],
                    "SELECT h.TransactionDate,c.CustomerName,pr.ProductCode,pr.ProductName,(SELECT x.SalePrice FROM CustomerProductPricingHistory x WHERE x.CustomerId=h.CustomerId AND x.ProductId=h.ProductId AND (x.TransactionDate<h.TransactionDate OR (x.TransactionDate=h.TransactionDate AND x.HistoryId<h.HistoryId)) ORDER BY x.TransactionDate DESC,x.HistoryId DESC LIMIT 1) AS PreviousPrice,h.SalePrice,h.DiscountPercentage,h.Quantity,h.InvoiceNumber FROM CustomerProductPricingHistory h JOIN Customers c ON c.CustomerId=h.CustomerId JOIN Products pr ON pr.ProductId=h.ProductId WHERE h.TransactionDate>=@StartDate AND h.TransactionDate<@EndExclusive AND (@CustomerId IS NULL OR h.CustomerId=@CustomerId) AND " + productFilter + " ORDER BY h.TransactionDate DESC,h.HistoryId DESC"),
                "CustomerStatement" => Def("Customer statement", [C("TransactionDate", "Date", "d"), C("PartyName", "Customer"), C("TransactionType", "Type"), C("ReferenceNumber", "Reference"), C("Description", "Description"), C("Debit", "Debit", "N2"), C("Credit", "Credit", "N2"), C("RunningBalance", "Balance", "N2")],
                    "WITH Party AS (SELECT CustomerId,CustomerName,OpeningBalance FROM Customers WHERE @CustomerId IS NULL OR CustomerId=@CustomerId), Opening AS (SELECT c.CustomerId,c.CustomerName,COALESCE((SELECT l.RunningBalance FROM CustomerLedger l WHERE l.CustomerId=c.CustomerId AND l.TransactionDate<@StartDate ORDER BY l.TransactionDate DESC,l.LedgerId DESC LIMIT 1),c.OpeningBalance,0) AS Balance FROM Party c), Closing AS (SELECT c.CustomerId,c.CustomerName,COALESCE((SELECT l.RunningBalance FROM CustomerLedger l WHERE l.CustomerId=c.CustomerId AND l.TransactionDate<@EndExclusive ORDER BY l.TransactionDate DESC,l.LedgerId DESC LIMIT 1),o.Balance) AS Balance FROM Party c JOIN Opening o ON o.CustomerId=c.CustomerId) SELECT @StartDate AS TransactionDate,CustomerName AS PartyName,'OPENING' AS TransactionType,'' AS ReferenceNumber,'Opening balance' AS Description,0 AS Debit,0 AS Credit,Balance AS RunningBalance FROM Opening UNION ALL SELECT l.TransactionDate,c.CustomerName,l.TransactionType,l.ReferenceNumber,l.Description,l.Debit,l.Credit,l.RunningBalance FROM CustomerLedger l JOIN Customers c ON c.CustomerId=l.CustomerId WHERE l.TransactionDate>=@StartDate AND l.TransactionDate<@EndExclusive AND (@CustomerId IS NULL OR l.CustomerId=@CustomerId) UNION ALL SELECT @EndDate,c.CustomerName,'CLOSING','','Closing balance',0,0,c.Balance FROM Closing c ORDER BY TransactionDate,PartyName,TransactionType"),
                "Suppliers" or "OutstandingPayables" => Def(key == "Suppliers" ? "Supplier list and balances" : "Outstanding payables", [C("SupplierCode", "Code"), C("SupplierName", "Supplier"), C("Phone", "Phone"), C("OutstandingBalance", "Payable", "N2"), C("Status", "Status")],
                    $"SELECT s.SupplierCode,s.SupplierName,s.Phone,s.OutstandingBalance,s.Status FROM Suppliers s WHERE {supplierFilter} AND ('{key}'<>'OutstandingPayables' OR s.OutstandingBalance>0) ORDER BY s.SupplierName"),
                "SupplierPurchaseHistory" => Def("Supplier purchase history", [C("PurchaseDate", "Date", "d"), C("InvoiceNumber", "Bill"), C("SubTotal", "Subtotal", "N2"), C("DiscountAmount", "Discount", "N2"), C("NetTotal", "Net purchases", "N2"), C("PaidAmount", "Paid", "N2"), C("RemainingBalance", "Outstanding", "N2"), C("PaymentStatus", "Status")],
                    $"SELECT p.PurchaseDate,p.InvoiceNumber,p.SubTotal,p.DiscountAmount,p.NetTotal,p.PaidAmount,p.RemainingBalance,CASE WHEN p.RemainingBalance<=0 THEN 'Paid' WHEN p.PaidAmount>0 THEN 'Partially paid' ELSE 'Outstanding' END AS PaymentStatus FROM Purchases p WHERE {purchaseFilter} AND {purchasesByProductExists} ORDER BY p.PurchaseDate DESC"),
                "SupplierPriceHistory" or "SupplierPriceChanges" => Def(key == "SupplierPriceHistory" ? "Supplier/product purchase-price history" : "Supplier/product price changes and discounts", [C("TransactionDate", "Date", "d"), C("SupplierName", "Supplier"), C("ProductCode", "Code"), C("ProductName", "Product"), C("PreviousPrice", "Previous rate", "N2"), C("PurchasePrice", "New rate", "N2"), C("DiscountPercentage", "Discount %", "N2"), C("Quantity", "Quantity", "N2"), C("InvoiceNumber", "Bill")],
                    "SELECT h.TransactionDate,s.SupplierName,pr.ProductCode,pr.ProductName,(SELECT x.PurchasePrice FROM SupplierProductPricingHistory x WHERE x.SupplierId=h.SupplierId AND x.ProductId=h.ProductId AND (x.TransactionDate<h.TransactionDate OR (x.TransactionDate=h.TransactionDate AND x.HistoryId<h.HistoryId)) ORDER BY x.TransactionDate DESC,x.HistoryId DESC LIMIT 1) AS PreviousPrice,h.PurchasePrice,h.DiscountPercentage,h.Quantity,h.InvoiceNumber FROM SupplierProductPricingHistory h JOIN Suppliers s ON s.SupplierId=h.SupplierId JOIN Products pr ON pr.ProductId=h.ProductId WHERE h.TransactionDate>=@StartDate AND h.TransactionDate<@EndExclusive AND (@SupplierId IS NULL OR h.SupplierId=@SupplierId) AND " + productFilter + " ORDER BY h.TransactionDate DESC,h.HistoryId DESC"),
                "SupplierStatement" => Def("Supplier statement", [C("TransactionDate", "Date", "d"), C("PartyName", "Supplier"), C("TransactionType", "Type"), C("ReferenceNumber", "Reference"), C("Description", "Description"), C("Debit", "Debit", "N2"), C("Credit", "Credit", "N2"), C("RunningBalance", "Balance", "N2")],
                    "WITH Party AS (SELECT SupplierId,SupplierName,OpeningBalance FROM Suppliers WHERE @SupplierId IS NULL OR SupplierId=@SupplierId), Opening AS (SELECT s.SupplierId,s.SupplierName,COALESCE((SELECT l.RunningBalance FROM SupplierLedger l WHERE l.SupplierId=s.SupplierId AND l.TransactionDate<@StartDate ORDER BY l.TransactionDate DESC,l.LedgerId DESC LIMIT 1),s.OpeningBalance,0) AS Balance FROM Party s), Closing AS (SELECT s.SupplierId,s.SupplierName,COALESCE((SELECT l.RunningBalance FROM SupplierLedger l WHERE l.SupplierId=s.SupplierId AND l.TransactionDate<@EndExclusive ORDER BY l.TransactionDate DESC,l.LedgerId DESC LIMIT 1),o.Balance) AS Balance FROM Party s JOIN Opening o ON o.SupplierId=s.SupplierId) SELECT @StartDate AS TransactionDate,SupplierName AS PartyName,'OPENING' AS TransactionType,'' AS ReferenceNumber,'Opening balance' AS Description,0 AS Debit,0 AS Credit,Balance AS RunningBalance FROM Opening UNION ALL SELECT l.TransactionDate,s.SupplierName,l.TransactionType,l.ReferenceNumber,l.Description,l.Debit,l.Credit,l.RunningBalance FROM SupplierLedger l JOIN Suppliers s ON s.SupplierId=l.SupplierId WHERE l.TransactionDate>=@StartDate AND l.TransactionDate<@EndExclusive AND (@SupplierId IS NULL OR l.SupplierId=@SupplierId) UNION ALL SELECT @EndDate,s.SupplierName,'CLOSING','','Closing balance',0,0,s.Balance FROM Closing s ORDER BY TransactionDate,PartyName,TransactionType"),
                "ExpensesByDate" => Def("Expenses by date range", [C("ExpenseDate", "Date", "d"), C("ExpenseCategory", "Category"), C("Description", "Description"), C("PaymentSource", "Paid from"), C("Amount", "Amount", "N2")],
                    $"SELECT e.ExpenseDate,e.ExpenseCategory,e.Description,e.PaymentSource,e.Amount FROM Expenses e WHERE {expenseFilter} ORDER BY e.ExpenseDate DESC,e.ExpenseId DESC"),
                "ExpensesByCategory" => Def("Expenses by category", [C("ExpenseCategory", "Category"), C("ExpenseCount", "Expenses", "N0"), C("TotalAmount", "Total amount", "N2")],
                    $"SELECT e.ExpenseCategory,COUNT(*) AS ExpenseCount,SUM(e.Amount) AS TotalAmount FROM Expenses e WHERE {expenseFilter} GROUP BY e.ExpenseCategory ORDER BY TotalAmount DESC"),
                "CashActivity" or "CashBalance" => AccountDefinition("Cash", "CashTransactions", "CashTransactionId", key == "CashBalance"),
                "BankActivity" or "BankBalance" => AccountDefinition("Bank", "BankTransactions", "BankTransactionId", key == "BankBalance"),
                "ReceiptsPayments" => Def("Receipts and payments", [C("TransactionDate", "Date", "d"), C("TransactionType", "Type"), C("ReferenceNumber", "Reference"), C("PartyName", "Customer / supplier"), C("PaymentMode", "Method"), C("Amount", "Amount", "N2"), C("Remarks", "Remarks")],
                    "SELECT r.ReceiptDate AS TransactionDate,'Receipt' AS TransactionType,r.ReceiptNumber AS ReferenceNumber,c.CustomerName AS PartyName,r.PaymentMode,r.Amount,r.Remarks FROM Receipts r JOIN Customers c ON c.CustomerId=r.CustomerId WHERE r.ReceiptDate>=@StartDate AND r.ReceiptDate<@EndExclusive AND (@CustomerId IS NULL OR r.CustomerId=@CustomerId) UNION ALL SELECT p.PaymentDate,'Supplier payment',p.PaymentNumber,s.SupplierName,p.PaymentMode,p.Amount,p.Remarks FROM Payments p JOIN Suppliers s ON s.SupplierId=p.SupplierId WHERE p.PaymentDate>=@StartDate AND p.PaymentDate<@EndExclusive AND (@SupplierId IS NULL OR p.SupplierId=@SupplierId) ORDER BY TransactionDate DESC"),
                "ReceivablesPayables" => Def("Outstanding receivables and payables", [C("AccountType", "Type"), C("PartyCode", "Code"), C("PartyName", "Customer / supplier"), C("Balance", "Outstanding", "N2")],
                    "SELECT 'Receivable' AS AccountType,CustomerCode AS PartyCode,CustomerName AS PartyName,OutstandingBalance AS Balance FROM Customers WHERE OutstandingBalance>0 AND (@CustomerId IS NULL OR CustomerId=@CustomerId) UNION ALL SELECT 'Payable',SupplierCode,SupplierName,OutstandingBalance FROM Suppliers WHERE OutstandingBalance>0 AND (@SupplierId IS NULL OR SupplierId=@SupplierId) ORDER BY AccountType,PartyName"),
                "ProductPriceList" => Def("Product default price list", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("BasePurchasePrice", "Default purchase", "N2"), C("DefaultSalePrice", "Default sale", "N2"), C("CurrentStock", "Stock", "N2")],
                    $"SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,pr.BasePurchasePrice,pr.DefaultSalePrice,pr.CurrentStock FROM Products pr LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId WHERE pr.Status='Active' AND {productFilter} ORDER BY pr.ProductName"),
                "ProductProfitability" => Def("Product profitability (historical transaction cost)", [C("ProductCode", "Code"), C("ProductName", "Product"), C("CategoryName", "Category"), C("SoldQuantity", "Sold qty", "N2"), C("ReturnedQuantity", "Returned qty", "N2"), C("NetSales", "Net sales", "N2"), C("NetCost", "Net cost", "N2"), C("GrossProfit", "Gross profit", "N2")],
                    $"WITH Sold AS (SELECT si.ProductId,SUM(si.Quantity) AS Qty,SUM(si.LineTotal) AS Revenue,SUM(si.Quantity*si.HistoricalCostPrice) AS Cost FROM SaleItems si JOIN Sales s ON s.SaleId=si.SaleId WHERE s.Status='Completed' AND s.SaleDate>=@StartDate AND s.SaleDate<@EndExclusive AND (@CustomerId IS NULL OR s.CustomerId=@CustomerId) GROUP BY si.ProductId), Returned AS (SELECT ri.ProductId,SUM(ri.Quantity) AS Qty,SUM(ri.LineTotal) AS Refund,SUM(ri.Quantity*COALESCE((SELECT SUM(si.HistoricalCostPrice*si.Quantity)/NULLIF(SUM(si.Quantity),0) FROM SaleItems si WHERE si.SaleId=r.OriginalSaleId AND si.ProductId=ri.ProductId),0)) AS Cost FROM SalesReturnItems ri JOIN SalesReturns r ON r.ReturnId=ri.ReturnId WHERE r.ReturnDate>=@StartDate AND r.ReturnDate<@EndExclusive AND (@CustomerId IS NULL OR r.CustomerId=@CustomerId) GROUP BY ri.ProductId) SELECT pr.ProductCode,pr.ProductName,COALESCE(cat.CategoryName,'') AS CategoryName,COALESCE(s.Qty,0) AS SoldQuantity,COALESCE(r.Qty,0) AS ReturnedQuantity,COALESCE(s.Revenue,0)-COALESCE(r.Refund,0) AS NetSales,COALESCE(s.Cost,0)-COALESCE(r.Cost,0) AS NetCost,(COALESCE(s.Revenue,0)-COALESCE(r.Refund,0))-(COALESCE(s.Cost,0)-COALESCE(r.Cost,0)) AS GrossProfit FROM Products pr LEFT JOIN Categories cat ON cat.CategoryId=pr.CategoryId LEFT JOIN Sold s ON s.ProductId=pr.ProductId LEFT JOIN Returned r ON r.ProductId=pr.ProductId WHERE (s.ProductId IS NOT NULL OR r.ProductId IS NOT NULL) AND {productFilter} ORDER BY GrossProfit DESC"),
                "ProfitAndLoss" => Def("Profit and loss", [C("Revenue", "Net revenue", "N2"), C("CostOfGoodsSold", "Net COGS", "N2"), C("GrossProfit", "Gross profit", "N2"), C("Expenses", "Expenses", "N2"), C("NetProfit", "Net profit", "N2")],
                    "WITH Totals AS (SELECT COALESCE((SELECT SUM(NetTotal) FROM Sales WHERE SaleDate>=@StartDate AND SaleDate<@EndExclusive AND Status='Completed'),0)-COALESCE((SELECT SUM(TotalRefundAmount) FROM SalesReturns WHERE ReturnDate>=@StartDate AND ReturnDate<@EndExclusive),0) AS Revenue,COALESCE((SELECT SUM(si.Quantity*si.HistoricalCostPrice) FROM SaleItems si JOIN Sales s ON s.SaleId=si.SaleId WHERE s.SaleDate>=@StartDate AND s.SaleDate<@EndExclusive AND s.Status='Completed'),0)-COALESCE((SELECT SUM(ri.Quantity*COALESCE((SELECT SUM(si.HistoricalCostPrice*si.Quantity)/NULLIF(SUM(si.Quantity),0) FROM SaleItems si WHERE si.SaleId=r.OriginalSaleId AND si.ProductId=ri.ProductId),0)) FROM SalesReturnItems ri JOIN SalesReturns r ON r.ReturnId=ri.ReturnId WHERE r.ReturnDate>=@StartDate AND r.ReturnDate<@EndExclusive),0) AS CostOfGoodsSold,COALESCE((SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate>=@StartDate AND ExpenseDate<@EndExclusive),0) AS Expenses) SELECT Revenue,CostOfGoodsSold,Revenue-CostOfGoodsSold AS GrossProfit,Expenses,Revenue-CostOfGoodsSold-Expenses AS NetProfit FROM Totals"),
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown operational report.")
            };
        }

        private static ReportDefinition AccountDefinition(string label, string table, string idColumn, bool balanceOnly)
        {
            var columns = new[] { C("TransactionDate", "Date", "d"), C("TransactionType", "Type"), C("ReferenceNumber", "Reference"), C("Description", "Description"), C("Debit", "Money in", "N2"), C("Credit", "Money out", "N2"), C("RunningBalance", "Balance", "N2") };
            var sql = $"WITH Opening AS (SELECT COALESCE((SELECT RunningBalance FROM {table} WHERE TransactionDate<@StartDate ORDER BY TransactionDate DESC,{idColumn} DESC LIMIT 1),0) AS Balance), Closing AS (SELECT COALESCE((SELECT RunningBalance FROM {table} WHERE TransactionDate<@EndExclusive ORDER BY TransactionDate DESC,{idColumn} DESC LIMIT 1),(SELECT Balance FROM Opening),0) AS Balance) {(balanceOnly ? "SELECT @EndDate AS TransactionDate,'CLOSING' AS TransactionType,'' AS ReferenceNumber,'Closing balance' AS Description,0 AS Debit,0 AS Credit,Balance AS RunningBalance FROM Closing" : $"SELECT @StartDate AS TransactionDate,'OPENING' AS TransactionType,'' AS ReferenceNumber,'Opening balance' AS Description,0 AS Debit,0 AS Credit,Balance AS RunningBalance FROM Opening UNION ALL SELECT TransactionDate,TransactionType,ReferenceNumber,Description,Debit,Credit,RunningBalance FROM {table} WHERE TransactionDate>=@StartDate AND TransactionDate<@EndExclusive UNION ALL SELECT @EndDate,'CLOSING','','Closing balance',0,0,Balance FROM Closing")} ORDER BY TransactionDate";
            return Def($"{label} {(balanceOnly ? "account balance" : "account activity and balance")}", columns, sql);
        }

        private static ReportDefinition Def(string title, OperationalReportColumn[] columns, string sql) => new(title, columns.ToList(), sql);
        private static OperationalReportColumn C(string key, string header, string? format = null) => new() { Key = key, Header = header, Format = format };
        private sealed record ReportDefinition(string Title, List<OperationalReportColumn> Columns, string Sql);

        public async Task<CustomerStatement> GetCustomerStatementAsync(int customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            const string customerSql = @"
                SELECT CustomerId, CustomerCode, CustomerName 
                FROM Customers 
                WHERE CustomerId = @CustomerId;";

            var customer = await connection.QuerySingleOrDefaultAsync<CustomerHeaderDto>(
                new CommandDefinition(customerSql, new { CustomerId = customerId }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            if (customer == null)
            {
                throw new KeyNotFoundException($"Customer with ID {customerId} was not found.");
            }

            const string openingBalanceSql = @"
                SELECT COALESCE(
                    (SELECT RunningBalance FROM CustomerLedger WHERE CustomerId = @CustomerId AND TransactionDate < @StartDate ORDER BY TransactionDate DESC, LedgerId DESC LIMIT 1),
                    (SELECT OpeningBalance FROM Customers WHERE CustomerId = @CustomerId),
                    0.0
                );";

            var openingBalance = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(openingBalanceSql, new { CustomerId = customerId, StartDate = startDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var queryEndDate = endDate.TimeOfDay == TimeSpan.Zero
                ? endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                : endDate;

            const string totalsSql = @"
                SELECT 
                    COALESCE(SUM(Debit), 0.0) AS TotalDebits, 
                    COALESCE(SUM(Credit), 0.0) AS TotalCredits 
                FROM CustomerLedger 
                WHERE CustomerId = @CustomerId AND TransactionDate >= @StartDate AND TransactionDate <= @EndDate;";

            var totals = await connection.QuerySingleAsync<StatementTotalsDto>(
                new CommandDefinition(totalsSql, new { CustomerId = customerId, StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            const string itemsSql = @"
                SELECT TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance 
                FROM CustomerLedger 
                WHERE CustomerId = @CustomerId AND TransactionDate >= @StartDate AND TransactionDate <= @EndDate 
                ORDER BY TransactionDate ASC, LedgerId ASC;";

            var items = await connection.QueryAsync<CustomerStatementItem>(
                new CommandDefinition(itemsSql, new { CustomerId = customerId, StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var closingBalance = openingBalance + totals.TotalDebits - totals.TotalCredits;

            return new CustomerStatement
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                StartDate = startDate,
                EndDate = endDate,
                OpeningBalance = openingBalance,
                TotalDebits = totals.TotalDebits,
                TotalCredits = totals.TotalCredits,
                ClosingBalance = closingBalance,
                Transactions = items.AsList()
            };
        }

        public async Task<ProfitAndLossSummary> GetProfitAndLossSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var queryEndDate = endDate.TimeOfDay == TimeSpan.Zero
                ? endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                : endDate;

            // 1. Net revenue: completed invoices less returns posted in the period.
            const string revenueSql = @"
                SELECT COALESCE((SELECT SUM(NetTotal) FROM Sales WHERE SaleDate >= @StartDate AND SaleDate <= @EndDate AND Status = 'Completed'), 0.0)
                     - COALESCE((SELECT SUM(TotalRefundAmount) FROM SalesReturns WHERE ReturnDate >= @StartDate AND ReturnDate <= @EndDate), 0.0);";

            var revenue = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(revenueSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 2. Net COGS uses immutable sale cost snapshots less returned sale-item cost.
            const string cogsSql = @"
                SELECT COALESCE((SELECT SUM(si.HistoricalCostPrice * si.Quantity)
                    FROM SaleItems si INNER JOIN Sales s ON si.SaleId = s.SaleId
                    WHERE s.SaleDate >= @StartDate AND s.SaleDate <= @EndDate AND s.Status = 'Completed'), 0.0)
                     - COALESCE((SELECT SUM(ri.Quantity * COALESCE((
                        SELECT SUM(original.HistoricalCostPrice * original.Quantity) / SUM(original.Quantity)
                        FROM SaleItems original WHERE original.SaleId = r.OriginalSaleId AND original.ProductId = ri.ProductId
                     ), 0.0))
                        FROM SalesReturnItems ri INNER JOIN SalesReturns r ON r.ReturnId = ri.ReturnId
                        WHERE r.ReturnDate >= @StartDate AND r.ReturnDate <= @EndDate), 0.0);";

            var cogs = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(cogsSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 3. Total Expenses within date range
            const string expensesSql = @"
                SELECT COALESCE(SUM(Amount), 0.0) 
                FROM Expenses 
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate;";

            var totalExpenses = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(expensesSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 4. Expenses categorized and aggregated in SQLite
            const string categoryExpensesSql = @"
                SELECT ExpenseCategory AS CategoryName, SUM(Amount) AS TotalAmount 
                FROM Expenses 
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate 
                GROUP BY ExpenseCategory 
                ORDER BY TotalAmount DESC;";

            var expenseCategories = await connection.QueryAsync<ExpenseCategorySummary>(
                new CommandDefinition(categoryExpensesSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var grossProfit = revenue - cogs;
            var netProfit = grossProfit - totalExpenses;

            return new ProfitAndLossSummary
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = revenue,
                CostOfGoodsSold = cogs,
                GrossProfit = grossProfit,
                TotalExpenses = totalExpenses,
                NetProfit = netProfit,
                ExpensesByCategory = expenseCategories.AsList()
            };
        }

        public Task<StockValuation> GetStockValuationAsync(CancellationToken cancellationToken = default)
        {
            return GetStockValuationAsync(DateTime.UtcNow, cancellationToken);
        }

        public async Task<StockValuation> GetStockValuationAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            const string totalsSql = @"
                SELECT 
                    COALESCE(SUM(CurrentStock * BasePurchasePrice), 0.0) AS TotalValuation,
                    COALESCE(SUM(CurrentStock * DefaultSalePrice), 0.0) AS TotalPotentialRetailValue,
                    COUNT(*) AS TotalItemsCount
                FROM Products
                WHERE Status = 'Active';";

            var totals = await connection.QuerySingleAsync<StockValuationTotalsDto>(
                new CommandDefinition(totalsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            const string itemsSql = @"
                SELECT 
                    ProductId, 
                    ProductCode, 
                    ProductName, 
                    CurrentStock, 
                    BasePurchasePrice AS UnitCost, 
                    (CurrentStock * BasePurchasePrice) AS TotalValuation, 
                    DefaultSalePrice, 
                    (CurrentStock * DefaultSalePrice) AS PotentialRetailValue 
                FROM Products 
                WHERE Status = 'Active' 
                ORDER BY ProductName ASC;";

            var items = await connection.QueryAsync<StockValuationItem>(
                new CommandDefinition(itemsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            return new StockValuation
            {
                ValuationDate = asOfDate,
                TotalValuation = totals.TotalValuation,
                TotalPotentialRetailValue = totals.TotalPotentialRetailValue,
                TotalItemsCount = totals.TotalItemsCount,
                Items = items.AsList()
            };
        }

        private sealed record CustomerHeaderDto
        {
            public int CustomerId { get; init; }
            public string CustomerCode { get; init; } = string.Empty;
            public string CustomerName { get; init; } = string.Empty;
        }

        private sealed record StatementTotalsDto
        {
            public decimal TotalDebits { get; init; }
            public decimal TotalCredits { get; init; }
        }

        private sealed record StockValuationTotalsDto
        {
            public decimal TotalValuation { get; init; }
            public decimal TotalPotentialRetailValue { get; init; }
            public int TotalItemsCount { get; init; }
        }
    }
}
