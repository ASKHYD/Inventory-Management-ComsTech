using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services
{
    public class TransactionCoordinator : ITransactionCoordinator
    {
        private readonly ISqliteConnectionFactory _connectionFactory;
        private readonly ISalesRepository _salesRepository;
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly IProductRepository _productRepository;
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly ICustomerLedgerRepository _customerLedgerRepository;
        private readonly ISupplierLedgerRepository _supplierLedgerRepository;
        private readonly ICustomerPricingHistoryRepository _customerPricingHistoryRepository;
        private readonly ISupplierPricingHistoryRepository _supplierPricingHistoryRepository;
        private readonly ICashTransactionRepository? _cashTransactionRepository;
        private readonly IBankTransactionRepository? _bankTransactionRepository;

        public TransactionCoordinator(
            ISqliteConnectionFactory connectionFactory,
            ISalesRepository salesRepository,
            IPurchaseRepository purchaseRepository,
            IProductRepository productRepository,
            IStockMovementRepository stockMovementRepository,
            ICustomerRepository customerRepository,
            ISupplierRepository supplierRepository,
            ICustomerLedgerRepository customerLedgerRepository,
            ISupplierLedgerRepository supplierLedgerRepository,
            ICustomerPricingHistoryRepository customerPricingHistoryRepository,
            ISupplierPricingHistoryRepository supplierPricingHistoryRepository,
            ICashTransactionRepository? cashTransactionRepository = null,
            IBankTransactionRepository? bankTransactionRepository = null)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _salesRepository = salesRepository ?? throw new ArgumentNullException(nameof(salesRepository));
            _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockMovementRepository = stockMovementRepository ?? throw new ArgumentNullException(nameof(stockMovementRepository));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _customerLedgerRepository = customerLedgerRepository ?? throw new ArgumentNullException(nameof(customerLedgerRepository));
            _supplierLedgerRepository = supplierLedgerRepository ?? throw new ArgumentNullException(nameof(supplierLedgerRepository));
            _customerPricingHistoryRepository = customerPricingHistoryRepository ?? throw new ArgumentNullException(nameof(customerPricingHistoryRepository));
            _supplierPricingHistoryRepository = supplierPricingHistoryRepository ?? throw new ArgumentNullException(nameof(supplierPricingHistoryRepository));
            _cashTransactionRepository = cashTransactionRepository;
            _bankTransactionRepository = bankTransactionRepository;
        }

        public async Task<int> PostSaleAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            if (sale == null)
            {
                throw new ArgumentNullException(nameof(sale));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Save Sale and SaleItems within transaction
                var saleId = await _salesRepository.AddAsync(sale, transaction, cancellationToken).ConfigureAwait(false);

                // Step 2: Deduct stock, log stock movement, and record pricing memory snapshot for each item
                if (sale.Items != null)
                {
                    foreach (var item in sale.Items)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId, transaction, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidOperationException($"Product with ID {item.ProductId} not found.");

                        var newStock = product.CurrentStock - item.Quantity;

                        await _productRepository.UpdateStockAsync(item.ProductId, newStock, transaction, cancellationToken).ConfigureAwait(false);

                        var movement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            MovementDate = sale.SaleDate == default ? DateTime.UtcNow : sale.SaleDate,
                            MovementType = "SALE",
                            ReferenceNumber = sale.InvoiceNumber,
                            QuantityIn = 0.0m,
                            QuantityOut = item.Quantity,
                            BalanceQuantity = newStock,
                            AdminId = sale.AdminId,
                            Remarks = string.IsNullOrWhiteSpace(sale.Remarks) ? $"Sale Invoice {sale.InvoiceNumber}" : sale.Remarks
                        };
                        await _stockMovementRepository.AddAsync(movement, transaction, cancellationToken).ConfigureAwait(false);

                        var pricingHistory = new CustomerProductPricingHistory
                        {
                            CustomerId = sale.CustomerId,
                            ProductId = item.ProductId,
                            SalePrice = item.HistoricalSalePrice,
                            SellingPercentage = item.SellingPercentage,
                            DiscountPercentage = item.DiscountPercentage,
                            Quantity = item.Quantity,
                            InvoiceNumber = sale.InvoiceNumber,
                            TransactionDate = sale.SaleDate == default ? DateTime.UtcNow : sale.SaleDate,
                            AdminId = sale.AdminId
                        };
                        await _customerPricingHistoryRepository.AddAsync(pricingHistory, transaction, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Step 3: Update Customer outstanding balance
                var customer = await _customerRepository.GetByIdAsync(sale.CustomerId, transaction, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Customer with ID {sale.CustomerId} not found.");

                var newCustBalance = customer.OutstandingBalance + sale.RemainingBalance;
                var custBalanceUpdated = await _customerRepository.UpdateBalanceAsync(sale.CustomerId, newCustBalance, transaction, cancellationToken).ConfigureAwait(false);
                if (!custBalanceUpdated)
                {
                    throw new InvalidOperationException($"Failed to update outstanding balance for Customer ID {sale.CustomerId}.");
                }

                // Step 4: Record CustomerLedger entry
                var ledgerEntry = new CustomerLedgerEntry
                {
                    CustomerId = sale.CustomerId,
                    TransactionDate = sale.SaleDate == default ? DateTime.UtcNow : sale.SaleDate,
                    TransactionType = "SALE",
                    ReferenceNumber = sale.InvoiceNumber,
                    Description = $"Sale Invoice {sale.InvoiceNumber}",
                    Debit = sale.NetTotal,
                    Credit = sale.PaidAmount,
                    RunningBalance = newCustBalance
                };
                await _customerLedgerRepository.AddAsync(ledgerEntry, transaction, cancellationToken).ConfigureAwait(false);

                if (sale.PaidAmount > 0 && _cashTransactionRepository != null && _bankTransactionRepository != null)
                {
                    var cash = string.Equals(sale.PaymentSource, "Cash", StringComparison.OrdinalIgnoreCase);
                    var current = cash
                        ? await _cashTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false)
                        : await _bankTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var tx = new CashTransaction
                    {
                        TransactionDate = sale.SaleDate == default ? DateTime.UtcNow : sale.SaleDate,
                        // BankTransactions uses the standard RECEIPT enum; CashTransactions
                        // also distinguishes point-of-sale takings as SALE_CASH.
                        TransactionType = cash ? "SALE_CASH" : "RECEIPT",
                        ReferenceNumber = sale.InvoiceNumber,
                        Description = $"Payment received for invoice {sale.InvoiceNumber}",
                        Debit = sale.PaidAmount,
                        Credit = 0m,
                        RunningBalance = current + sale.PaidAmount,
                        AdminId = sale.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    if (cash) await _cashTransactionRepository.AddAsync(tx, transaction, cancellationToken).ConfigureAwait(false);
                    else await _bankTransactionRepository.AddAsync(new BankTransaction
                    {
                        TransactionDate = tx.TransactionDate, TransactionType = tx.TransactionType,
                        ReferenceNumber = tx.ReferenceNumber, Description = tx.Description, Debit = tx.Debit,
                        Credit = tx.Credit, RunningBalance = tx.RunningBalance, AdminId = tx.AdminId, CreatedAt = tx.CreatedAt
                    }, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
                    VALUES (@AdminId, 'CREATE', 'Sales', @Reference, @NewValue);",
                    new { sale.AdminId, Reference = sale.InvoiceNumber, NewValue = $"CustomerId={sale.CustomerId}; NetTotal={sale.NetTotal}" },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Step 5: Atomically commit all changes
                transaction.Commit();
                return saleId;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rbEx)
                {
                    ex.Data["RollbackError"] = rbEx.Message;
                }

                throw;
            }
        }

        public async Task<int> PostPurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default)
        {
            if (purchase == null)
            {
                throw new ArgumentNullException(nameof(purchase));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Save Purchase and PurchaseItems within transaction
                var purchaseId = await _purchaseRepository.AddAsync(purchase, transaction, cancellationToken).ConfigureAwait(false);

                // Step 2: Increment stock, log stock movement, and record supplier pricing history
                if (purchase.Items != null)
                {
                    foreach (var item in purchase.Items)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId, transaction, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidOperationException($"Product with ID {item.ProductId} not found.");

                        var newStock = product.CurrentStock + item.Quantity;

                        await _productRepository.UpdateStockAsync(item.ProductId, newStock, transaction, cancellationToken).ConfigureAwait(false);

                        var movement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            MovementDate = purchase.PurchaseDate == default ? DateTime.UtcNow : purchase.PurchaseDate,
                            MovementType = "PURCHASE",
                            ReferenceNumber = purchase.InvoiceNumber,
                            QuantityIn = item.Quantity,
                            QuantityOut = 0.0m,
                            BalanceQuantity = newStock,
                            AdminId = purchase.AdminId,
                            Remarks = string.IsNullOrWhiteSpace(purchase.Remarks) ? $"Purchase Invoice {purchase.InvoiceNumber}" : purchase.Remarks
                        };
                        await _stockMovementRepository.AddAsync(movement, transaction, cancellationToken).ConfigureAwait(false);

                        var pricingHistory = new SupplierProductPricingHistory
                        {
                            SupplierId = purchase.SupplierId,
                            ProductId = item.ProductId,
                            PurchasePrice = item.HistoricalPurchasePrice,
                            PurchasePercentage = item.PurchasePercentage,
                            DiscountPercentage = item.DiscountPercentage,
                            Quantity = item.Quantity,
                            InvoiceNumber = purchase.InvoiceNumber,
                            TransactionDate = purchase.PurchaseDate == default ? DateTime.UtcNow : purchase.PurchaseDate,
                            AdminId = purchase.AdminId
                        };
                        await _supplierPricingHistoryRepository.AddAsync(pricingHistory, transaction, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Step 3: Update Supplier outstanding balance
                var supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId, transaction, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Supplier with ID {purchase.SupplierId} not found.");

                var newSuppBalance = supplier.OutstandingBalance + purchase.RemainingBalance;
                var suppBalanceUpdated = await _supplierRepository.UpdateBalanceAsync(purchase.SupplierId, newSuppBalance, transaction, cancellationToken).ConfigureAwait(false);
                if (!suppBalanceUpdated)
                {
                    throw new InvalidOperationException($"Failed to update outstanding balance for Supplier ID {purchase.SupplierId}.");
                }

                // Step 4: Record SupplierLedger entry
                var ledgerEntry = new SupplierLedgerEntry
                {
                    SupplierId = purchase.SupplierId,
                    TransactionDate = purchase.PurchaseDate == default ? DateTime.UtcNow : purchase.PurchaseDate,
                    TransactionType = "PURCHASE",
                    ReferenceNumber = purchase.InvoiceNumber,
                    Description = $"Purchase Invoice {purchase.InvoiceNumber}",
                    Debit = purchase.PaidAmount,
                    Credit = purchase.NetTotal,
                    RunningBalance = newSuppBalance
                };
                await _supplierLedgerRepository.AddAsync(ledgerEntry, transaction, cancellationToken).ConfigureAwait(false);

                if (purchase.PaidAmount > 0 && _cashTransactionRepository != null && _bankTransactionRepository != null)
                {
                    var cash = string.Equals(purchase.PaymentSource, "Cash", StringComparison.OrdinalIgnoreCase);
                    var current = cash
                        ? await _cashTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false)
                        : await _bankTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var tx = new CashTransaction
                    {
                        TransactionDate = purchase.PurchaseDate == default ? DateTime.UtcNow : purchase.PurchaseDate,
                        // BankTransactions records supplier outflows as PAYMENT;
                        // CashTransactions has a dedicated PURCHASE_CASH value.
                        TransactionType = cash ? "PURCHASE_CASH" : "PAYMENT",
                        ReferenceNumber = purchase.InvoiceNumber,
                        Description = $"Payment made for bill {purchase.InvoiceNumber}",
                        Debit = 0m,
                        Credit = purchase.PaidAmount,
                        RunningBalance = current - purchase.PaidAmount,
                        AdminId = purchase.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    if (cash) await _cashTransactionRepository.AddAsync(tx, transaction, cancellationToken).ConfigureAwait(false);
                    else await _bankTransactionRepository.AddAsync(new BankTransaction
                    {
                        TransactionDate = tx.TransactionDate, TransactionType = tx.TransactionType,
                        ReferenceNumber = tx.ReferenceNumber, Description = tx.Description, Debit = tx.Debit,
                        Credit = tx.Credit, RunningBalance = tx.RunningBalance, AdminId = tx.AdminId, CreatedAt = tx.CreatedAt
                    }, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
                    VALUES (@AdminId, 'CREATE', 'Purchases', @Reference, @NewValue);",
                    new { purchase.AdminId, Reference = purchase.InvoiceNumber, NewValue = $"SupplierId={purchase.SupplierId}; NetTotal={purchase.NetTotal}" },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Step 5: Atomically commit all changes
                transaction.Commit();
                return purchaseId;
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rbEx)
                {
                    ex.Data["RollbackError"] = rbEx.Message;
                }

                throw;
            }
        }

        public async Task<int> PostReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.ReturnNumber)) throw new InvalidOperationException("A return reference number is required.");
            if (string.IsNullOrWhiteSpace(request.OriginalInvoiceNumber) || request.Items.Count == 0)
                throw new InvalidOperationException("Choose an original invoice and at least one item to return.");
            if (request.Items.Any(i => i.Quantity <= 0 || i.RefundRate < 0) || request.Items.GroupBy(i => i.ProductId).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Return quantities must be positive and rates cannot be negative.");

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var transaction = connection.BeginTransaction();
            try
            {
                var saleReturn = request.IsSaleReturn;
                var source = saleReturn
                    ? await connection.QuerySingleOrDefaultAsync<ReturnSource>(new CommandDefinition("SELECT SaleId AS SourceId, CustomerId AS PartyId FROM Sales WHERE InvoiceNumber = @Invoice COLLATE NOCASE", new { Invoice = request.OriginalInvoiceNumber }, transaction, cancellationToken: cancellationToken))
                    : await connection.QuerySingleOrDefaultAsync<ReturnSource>(new CommandDefinition("SELECT PurchaseId AS SourceId, SupplierId AS PartyId FROM Purchases WHERE InvoiceNumber = @Invoice COLLATE NOCASE", new { Invoice = request.OriginalInvoiceNumber }, transaction, cancellationToken: cancellationToken));
                if (source == null) throw new InvalidOperationException("The original invoice was not found.");

                int sourceId = source.SourceId;
                int partyId = source.PartyId;
                decimal total = 0m;
                foreach (var line in request.Items)
                {
                    var sourceLine = saleReturn
                        ? await connection.QuerySingleOrDefaultAsync<ReturnInvoiceLine>(new CommandDefinition("SELECT ProductId, SUM(Quantity) AS Quantity, SUM(LineTotal)/SUM(Quantity) AS Rate FROM SaleItems WHERE SaleId=@SourceId AND ProductId=@ProductId GROUP BY ProductId", new { SourceId = sourceId, line.ProductId }, transaction, cancellationToken: cancellationToken))
                        : await connection.QuerySingleOrDefaultAsync<ReturnInvoiceLine>(new CommandDefinition("SELECT ProductId, SUM(Quantity) AS Quantity, SUM(LineTotal)/SUM(Quantity) AS Rate FROM PurchaseItems WHERE PurchaseId=@SourceId AND ProductId=@ProductId GROUP BY ProductId", new { SourceId = sourceId, line.ProductId }, transaction, cancellationToken: cancellationToken));
                    if (sourceLine == null) throw new InvalidOperationException("A selected product is not on the original invoice.");

                    var returnedBefore = saleReturn
                        ? await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT COALESCE(SUM(i.Quantity),0) FROM SalesReturnItems i JOIN SalesReturns r ON r.ReturnId=i.ReturnId WHERE r.OriginalSaleId=@SourceId AND i.ProductId=@ProductId", new { SourceId = sourceId, line.ProductId }, transaction, cancellationToken: cancellationToken))
                        : await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT COALESCE(SUM(i.Quantity),0) FROM PurchaseReturnItems i JOIN PurchaseReturns r ON r.ReturnId=i.ReturnId WHERE r.OriginalPurchaseId=@SourceId AND i.ProductId=@ProductId", new { SourceId = sourceId, line.ProductId }, transaction, cancellationToken: cancellationToken));
                    var sourceQty = (decimal)sourceLine.Quantity;
                    var sourceRate = (decimal)sourceLine.Rate;
                    if (line.Quantity > sourceQty - returnedBefore)
                        throw new InvalidOperationException($"Return quantity exceeds the remaining quantity from the original invoice for product {line.ProductId}.");
                    if (line.RefundRate > sourceRate)
                        throw new InvalidOperationException("Return rate cannot exceed the original invoice rate.");

                    var product = await _productRepository.GetByIdAsync(line.ProductId, transaction, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("A product on the return could not be found.");
                    var stock = saleReturn ? product.CurrentStock + line.Quantity : product.CurrentStock - line.Quantity;
                    if (stock < 0) throw new InvalidOperationException("There is not enough current stock to return this purchase quantity.");
                    if (!await _productRepository.UpdateStockAsync(line.ProductId, stock, transaction, cancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("Stock could not be updated for a returned product.");
                    total += line.Quantity * line.RefundRate;
                }

                var now = request.ReturnDate == default ? DateTime.Today : request.ReturnDate;
                var returnId = saleReturn
                    ? await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO SalesReturns (ReturnNumber, OriginalSaleId, CustomerId, ReturnDate, TotalRefundAmount, AdminId, Remarks) VALUES (@Number,@SourceId,@PartyId,@Date,@Total,@AdminId,@Remarks); SELECT last_insert_rowid();", new { Number = request.ReturnNumber, SourceId = sourceId, PartyId = partyId, Date = now, Total = total, request.AdminId, request.Remarks }, transaction, cancellationToken: cancellationToken))
                    : await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"INSERT INTO PurchaseReturns (ReturnNumber, OriginalPurchaseId, SupplierId, ReturnDate, TotalRefundAmount, AdminId, Remarks) VALUES (@Number,@SourceId,@PartyId,@Date,@Total,@AdminId,@Remarks); SELECT last_insert_rowid();", new { Number = request.ReturnNumber, SourceId = sourceId, PartyId = partyId, Date = now, Total = total, request.AdminId, request.Remarks }, transaction, cancellationToken: cancellationToken));

                foreach (var line in request.Items)
                {
                    var lineTotal = line.Quantity * line.RefundRate;
                    if (saleReturn)
                        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO SalesReturnItems (ReturnId,ProductId,Quantity,RefundRate,LineTotal) VALUES (@returnId,@ProductId,@Quantity,@RefundRate,@LineTotal)", new { returnId, line.ProductId, line.Quantity, line.RefundRate, LineTotal = lineTotal }, transaction, cancellationToken: cancellationToken));
                    else
                        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO PurchaseReturnItems (ReturnId,ProductId,Quantity,RefundRate,LineTotal) VALUES (@returnId,@ProductId,@Quantity,@RefundRate,@LineTotal)", new { returnId, line.ProductId, line.Quantity, line.RefundRate, LineTotal = lineTotal }, transaction, cancellationToken: cancellationToken));

                    var product = await _productRepository.GetByIdAsync(line.ProductId, transaction, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("A product on the return could not be found.");
                    var newStock = product.CurrentStock;
                    var movement = new StockMovement { ProductId = line.ProductId, MovementDate = now, MovementType = saleReturn ? "SALE_RETURN" : "PURCHASE_RETURN", ReferenceNumber = request.ReturnNumber, QuantityIn = saleReturn ? line.Quantity : 0m, QuantityOut = saleReturn ? 0m : line.Quantity, BalanceQuantity = newStock, AdminId = request.AdminId, Remarks = request.Remarks ?? "Return" };
                    await _stockMovementRepository.AddAsync(movement, transaction, cancellationToken).ConfigureAwait(false);
                }

                if (saleReturn)
                {
                    var customer = await _customerRepository.GetByIdAsync(partyId, transaction, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The customer on the original sale no longer exists.");
                    var balance = customer.OutstandingBalance - total;
                    if (!await _customerRepository.UpdateBalanceAsync(partyId, balance, transaction, cancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("The customer balance could not be updated.");
                    await _customerLedgerRepository.AddAsync(new CustomerLedgerEntry { CustomerId = partyId, TransactionDate = now, TransactionType = "SALE_RETURN", ReferenceNumber = request.ReturnNumber, Description = $"Return against {request.OriginalInvoiceNumber}", Credit = total, RunningBalance = balance }, transaction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var supplier = await _supplierRepository.GetByIdAsync(partyId, transaction, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The supplier on the original purchase no longer exists.");
                    var balance = supplier.OutstandingBalance - total;
                    if (!await _supplierRepository.UpdateBalanceAsync(partyId, balance, transaction, cancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("The supplier balance could not be updated.");
                    await _supplierLedgerRepository.AddAsync(new SupplierLedgerEntry { SupplierId = partyId, TransactionDate = now, TransactionType = "PURCHASE_RETURN", ReferenceNumber = request.ReturnNumber, Description = $"Return against {request.OriginalInvoiceNumber}", Debit = total, RunningBalance = balance }, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition("INSERT INTO AuditLogs (AdminId,Action,ModuleName,RecordReference,NewValue) VALUES (@AdminId,'CREATE',@Module,@Reference,@Value)", new { request.AdminId, Module = saleReturn ? "SalesReturns" : "PurchaseReturns", Reference = request.ReturnNumber, Value = $"Original={request.OriginalInvoiceNumber}; Total={total}" }, transaction, cancellationToken: cancellationToken));
                transaction.Commit();
                return returnId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private sealed class ReturnSource
        {
            public int SourceId { get; set; }
            public int PartyId { get; set; }
        }

        private sealed class ReturnInvoiceLine
        {
            public int ProductId { get; set; }
            public decimal Quantity { get; set; }
            public decimal Rate { get; set; }
        }
    }
}
