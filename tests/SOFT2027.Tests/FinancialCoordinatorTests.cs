using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Repositories;
using SOFT2027.Data.Services;
using Xunit;

namespace SOFT2027.Tests
{
    public sealed class FinancialCoordinatorTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly IFinancialCoordinator _sut;

        private int _testAdminId;
        private int _testCustomerId;
        private int _testSupplierId;

        public FinancialCoordinatorTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_fin_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);

            var receiptRepo = new ReceiptRepository(_factory);
            var paymentRepo = new PaymentRepository(_factory);
            var expenseRepo = new ExpenseRepository(_factory);
            var cashRepo = new CashTransactionRepository(_factory);
            var bankRepo = new BankTransactionRepository(_factory);
            var customerRepo = new CustomerRepository(_factory);
            var supplierRepo = new SupplierRepository(_factory);
            var customerLedgerRepo = new CustomerLedgerRepository(_factory);
            var supplierLedgerRepo = new SupplierLedgerRepository(_factory);

            _sut = new FinancialCoordinator(
                _factory,
                receiptRepo,
                paymentRepo,
                expenseRepo,
                cashRepo,
                bankRepo,
                customerRepo,
                supplierRepo,
                customerLedgerRepo,
                supplierLedgerRepo);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
            SeedTestData();
        }

        private void SeedTestData()
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            _testAdminId = connection.ExecuteScalar<int>(
                "INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin1', 'hash', 'Admin User'); SELECT last_insert_rowid();");

            _testCustomerId = connection.ExecuteScalar<int>(
                "INSERT INTO Customers (CustomerCode, CustomerName, OpeningBalance, OutstandingBalance) VALUES ('CUST-001', 'Test Customer', 0.0, 1000.0); SELECT last_insert_rowid();");

            _testSupplierId = connection.ExecuteScalar<int>(
                "INSERT INTO Suppliers (SupplierCode, SupplierName, OpeningBalance, OutstandingBalance) VALUES ('SUPP-001', 'Test Supplier', 0.0, 800.0); SELECT last_insert_rowid();");

            // Seed initial cash transaction of 500
            connection.Execute(
                @"INSERT INTO CashTransactions (TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance, AdminId) 
                  VALUES (CURRENT_TIMESTAMP, 'OPENING', 'OPENING-001', 'Initial Cash', 500.0, 0.0, 500.0, @AdminId);",
                new { AdminId = _testAdminId });
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore transient file lock on cleanup
                }
            }
        }

        [Fact]
        public async Task PostReceiptAsync_ShouldAtomicallyUpdate_ReceiptCustomerLedgerBalanceAndCash_WhenCashMode()
        {
            // Arrange
            var receipt = new Receipt
            {
                ReceiptNumber = "REC-2026-0001",
                CustomerId = _testCustomerId,
                ReceiptDate = DateTime.UtcNow,
                Amount = 400.0m,
                PaymentMode = "Cash",
                ReferenceDetails = "Cash payment on counter",
                AdminId = _testAdminId,
                Remarks = "Test Customer Receipt"
            };

            // Act
            var receiptId = await _sut.PostReceiptAsync(receipt, CancellationToken.None);

            // Assert
            receiptId.Should().BeGreaterThan(0);

            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            // 1. Verify Receipt record
            var dbReceipt = await verifyConn.QuerySingleOrDefaultAsync<Receipt>(
                "SELECT * FROM Receipts WHERE ReceiptId = @ReceiptId;", new { ReceiptId = receiptId });
            dbReceipt.Should().NotBeNull();
            dbReceipt!.ReceiptNumber.Should().Be("REC-2026-0001");
            dbReceipt.Amount.Should().Be(400.0m);

            // 2. Verify Customer Outstanding Balance reduced by 400 (1000 - 400 = 600)
            var custBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
            custBalance.Should().Be(600.0m);

            // 3. Verify CustomerLedger credited
            var ledger = await verifyConn.QuerySingleOrDefaultAsync<CustomerLedgerEntry>(
                "SELECT * FROM CustomerLedger WHERE ReferenceNumber = @Ref;", new { Ref = "REC-2026-0001" });
            ledger.Should().NotBeNull();
            ledger!.TransactionType.Should().Be("RECEIPT");
            ledger.Credit.Should().Be(400.0m);
            ledger.Debit.Should().Be(0.0m);
            ledger.RunningBalance.Should().Be(600.0m);

            // 4. Verify CashTransaction debit (+400 cash, running balance 500 + 400 = 900)
            var cashTx = await verifyConn.QuerySingleOrDefaultAsync<CashTransaction>(
                "SELECT * FROM CashTransactions WHERE ReferenceNumber = @Ref;", new { Ref = "REC-2026-0001" });
            cashTx.Should().NotBeNull();
            cashTx!.TransactionType.Should().Be("RECEIPT");
            cashTx.Debit.Should().Be(400.0m);
            cashTx.Credit.Should().Be(0.0m);
            cashTx.RunningBalance.Should().Be(900.0m);
        }

        [Fact]
        public async Task PostReceiptAsync_ShouldRollbackEntireTransaction_WhenCustomerDoesNotExistOrStepFails()
        {
            // Silent Failure Hunter Mandate:
            // Ensure invalid conditions cause 100% rollback across Receipts, Customer, CustomerLedger, and CashTransactions.

            // Arrange: Capture pre-transaction state
            using (var preConn = _factory.CreateConnection())
            {
                preConn.Open();
                var preBalance = await preConn.ExecuteScalarAsync<decimal>(
                    "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
                var preCashCount = await preConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CashTransactions;");
                preBalance.Should().Be(1000.0m);
                preCashCount.Should().Be(1);
            }

            var invalidReceipt = new Receipt
            {
                ReceiptNumber = "REC-FAIL-001",
                CustomerId = 999999, // Non-existent foreign key
                ReceiptDate = DateTime.UtcNow,
                Amount = 300.0m,
                PaymentMode = "Cash",
                AdminId = _testAdminId
            };

            // Act
            Func<Task> act = async () => await _sut.PostReceiptAsync(invalidReceipt, CancellationToken.None);

            // Assert: Must throw genuine database exception (NOT NotImplementedException)
            var ex = await act.Should().ThrowAsync<Exception>();
            ex.Which.Should().NotBeOfType<NotImplementedException>("genuine database failure must trigger rollback, not an unimplemented stub");

            // Assert: Verify complete ACID rollback
            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            var receiptCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Receipts WHERE ReceiptNumber = 'REC-FAIL-001';");
            receiptCount.Should().Be(0, "Receipt row must not exist after rollback");

            var custBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
            custBalance.Should().Be(1000.0m, "Customer balance must remain strictly at 1000.0m");

            var ledgerCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CustomerLedger WHERE ReferenceNumber = 'REC-FAIL-001';");
            ledgerCount.Should().Be(0, "No customer ledger entries must exist for failed receipt");

            var cashCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CashTransactions;");
            cashCount.Should().Be(1, "Cash transaction count must remain unchanged at 1");
        }

        [Fact]
        public async Task PostPaymentAsync_ShouldAtomicallyUpdate_PaymentSupplierLedgerBalanceAndCash_WhenCashMode()
        {
            // Arrange
            var payment = new Payment
            {
                PaymentNumber = "PAY-2026-0001",
                SupplierId = _testSupplierId,
                PaymentDate = DateTime.UtcNow,
                Amount = 300.0m,
                PaymentMode = "Cash",
                ReferenceDetails = "Cash payment voucher",
                AdminId = _testAdminId,
                Remarks = "Test Supplier Payment"
            };

            // Act
            var paymentId = await _sut.PostPaymentAsync(payment, CancellationToken.None);

            // Assert
            paymentId.Should().BeGreaterThan(0);

            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            // 1. Verify Payment record
            var dbPayment = await verifyConn.QuerySingleOrDefaultAsync<Payment>(
                "SELECT * FROM Payments WHERE PaymentId = @PaymentId;", new { PaymentId = paymentId });
            dbPayment.Should().NotBeNull();
            dbPayment!.PaymentNumber.Should().Be("PAY-2026-0001");
            dbPayment.Amount.Should().Be(300.0m);

            // 2. Verify Supplier Outstanding Balance reduced by 300 (800 - 300 = 500)
            var suppBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = @SupplierId;", new { SupplierId = _testSupplierId });
            suppBalance.Should().Be(500.0m);

            // 3. Verify SupplierLedger debited (decreases payable)
            var ledger = await verifyConn.QuerySingleOrDefaultAsync<SupplierLedgerEntry>(
                "SELECT * FROM SupplierLedger WHERE ReferenceNumber = @Ref;", new { Ref = "PAY-2026-0001" });
            ledger.Should().NotBeNull();
            ledger!.TransactionType.Should().Be("PAYMENT");
            ledger.Debit.Should().Be(300.0m);
            ledger.Credit.Should().Be(0.0m);
            ledger.RunningBalance.Should().Be(500.0m);

            // 4. Verify CashTransaction credit (Cash out 300, running balance 500 - 300 = 200)
            var cashTx = await verifyConn.QuerySingleOrDefaultAsync<CashTransaction>(
                "SELECT * FROM CashTransactions WHERE ReferenceNumber = @Ref;", new { Ref = "PAY-2026-0001" });
            cashTx.Should().NotBeNull();
            cashTx!.TransactionType.Should().Be("PAYMENT");
            cashTx.Credit.Should().Be(300.0m);
            cashTx.Debit.Should().Be(0.0m);
            cashTx.RunningBalance.Should().Be(200.0m);
        }

        [Fact]
        public async Task PostPaymentAsync_ShouldRollbackEntireTransaction_WhenSupplierDoesNotExistOrStepFails()
        {
            // Arrange
            var invalidPayment = new Payment
            {
                PaymentNumber = "PAY-FAIL-001",
                SupplierId = 888888, // Non-existent foreign key
                PaymentDate = DateTime.UtcNow,
                Amount = 250.0m,
                PaymentMode = "Cash",
                AdminId = _testAdminId
            };

            // Act
            Func<Task> act = async () => await _sut.PostPaymentAsync(invalidPayment, CancellationToken.None);

            // Assert
            var ex = await act.Should().ThrowAsync<Exception>();
            ex.Which.Should().NotBeOfType<NotImplementedException>("genuine database failure must trigger rollback, not an unimplemented stub");

            // Assert: Verify complete ACID rollback
            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            var paymentCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Payments WHERE PaymentNumber = 'PAY-FAIL-001';");
            paymentCount.Should().Be(0, "Payment row must not exist after rollback");

            var suppBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = @SupplierId;", new { SupplierId = _testSupplierId });
            suppBalance.Should().Be(800.0m, "Supplier balance must remain strictly at 800.0m");

            var ledgerCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SupplierLedger WHERE ReferenceNumber = 'PAY-FAIL-001';");
            ledgerCount.Should().Be(0, "No supplier ledger entries must exist for failed payment");

            var cashCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CashTransactions;");
            cashCount.Should().Be(1, "Cash transaction count must remain unchanged at 1");
        }

        [Fact]
        public async Task PostExpenseAsync_ShouldAtomicallyRecordExpenseAndCash_WhenCashSource()
        {
            // Arrange
            var expense = new Expense
            {
                ExpenseDate = DateTime.UtcNow,
                ExpenseCategory = "Utilities",
                Amount = 150.0m,
                Description = "Shop electricity bill payment",
                PaymentSource = "Cash",
                AdminId = _testAdminId
            };

            // Act
            var expenseId = await _sut.PostExpenseAsync(expense, CancellationToken.None);

            // Assert
            expenseId.Should().BeGreaterThan(0);

            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            // 1. Verify Expense record
            var dbExpense = await verifyConn.QuerySingleOrDefaultAsync<Expense>(
                "SELECT * FROM Expenses WHERE ExpenseId = @ExpenseId;", new { ExpenseId = expenseId });
            dbExpense.Should().NotBeNull();
            dbExpense!.ExpenseCategory.Should().Be("Utilities");
            dbExpense.Amount.Should().Be(150.0m);

            // 2. Verify CashTransaction credit (500 - 150 = 350)
            var cashTx = await verifyConn.QuerySingleOrDefaultAsync<CashTransaction>(
                "SELECT * FROM CashTransactions WHERE Description LIKE '%Shop electricity bill%' OR TransactionType = 'EXPENSE' ORDER BY CashTransactionId DESC LIMIT 1;");
            cashTx.Should().NotBeNull();
            cashTx!.Credit.Should().Be(150.0m);
            cashTx.RunningBalance.Should().Be(350.0m);
        }

        [Fact]
        public async Task RepeatedPartialReceiptsAndPayments_ShouldReconcileCashBankAndPartyBalances()
        {
            await _sut.PostReceiptAsync(new Receipt { ReceiptNumber = "REC-MULTI-CASH", CustomerId = _testCustomerId, ReceiptDate = DateTime.UtcNow, Amount = 200m, PaymentMode = "Cash", AdminId = _testAdminId });
            await _sut.PostReceiptAsync(new Receipt { ReceiptNumber = "REC-MULTI-BANK", CustomerId = _testCustomerId, ReceiptDate = DateTime.UtcNow, Amount = 300m, PaymentMode = "OnlineTransfer", AdminId = _testAdminId });
            await _sut.PostPaymentAsync(new Payment { PaymentNumber = "PAY-MULTI-CASH", SupplierId = _testSupplierId, PaymentDate = DateTime.UtcNow, Amount = 100m, PaymentMode = "Cash", AdminId = _testAdminId });
            await _sut.PostPaymentAsync(new Payment { PaymentNumber = "PAY-MULTI-BANK", SupplierId = _testSupplierId, PaymentDate = DateTime.UtcNow, Amount = 250m, PaymentMode = "Cheque", AdminId = _testAdminId });

            using var connection = _factory.CreateConnection();
            connection.Open();
            (await connection.ExecuteScalarAsync<decimal>("SELECT OutstandingBalance FROM Customers WHERE CustomerId=@Id", new { Id = _testCustomerId })).Should().Be(500m);
            (await connection.ExecuteScalarAsync<decimal>("SELECT OutstandingBalance FROM Suppliers WHERE SupplierId=@Id", new { Id = _testSupplierId })).Should().Be(450m);
            (await connection.ExecuteScalarAsync<decimal>("SELECT RunningBalance FROM CashTransactions ORDER BY CashTransactionId DESC LIMIT 1")).Should().Be(600m);
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BankTransactions")).Should().Be(2);
            (await connection.ExecuteScalarAsync<decimal>("SELECT RunningBalance FROM BankTransactions ORDER BY BankTransactionId DESC LIMIT 1")).Should().Be(50m);
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CustomerLedger WHERE ReferenceNumber LIKE 'REC-MULTI-%'")).Should().Be(2);
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SupplierLedger WHERE ReferenceNumber LIKE 'PAY-MULTI-%'")).Should().Be(2);
        }

        [Fact]
        public async Task ReceiptAndPaymentCannotOverSettleAndFailedAttemptLeavesNoEntries()
        {
            var receipt = new Receipt { ReceiptNumber = "REC-OVER", CustomerId = _testCustomerId, ReceiptDate = DateTime.UtcNow, Amount = 1001m, PaymentMode = "Cash", AdminId = _testAdminId };
            Func<Task> receiptAction = () => _sut.PostReceiptAsync(receipt);
            await receiptAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot exceed*");

            var payment = new Payment { PaymentNumber = "PAY-OVER", SupplierId = _testSupplierId, PaymentDate = DateTime.UtcNow, Amount = 801m, PaymentMode = "Cash", AdminId = _testAdminId };
            Func<Task> paymentAction = () => _sut.PostPaymentAsync(payment);
            await paymentAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot exceed*");

            using var connection = _factory.CreateConnection();
            connection.Open();
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Receipts WHERE ReceiptNumber='REC-OVER'")).Should().Be(0);
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Payments WHERE PaymentNumber='PAY-OVER'")).Should().Be(0);
            (await connection.ExecuteScalarAsync<decimal>("SELECT OutstandingBalance FROM Customers WHERE CustomerId=@Id", new { Id = _testCustomerId })).Should().Be(1000m);
            (await connection.ExecuteScalarAsync<decimal>("SELECT OutstandingBalance FROM Suppliers WHERE SupplierId=@Id", new { Id = _testSupplierId })).Should().Be(800m);
        }
    }
}
