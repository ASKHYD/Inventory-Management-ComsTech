using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services
{
    public class FinancialCoordinator : IFinancialCoordinator
    {
        private readonly ISqliteConnectionFactory _connectionFactory;
        private readonly IReceiptRepository _receiptRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IExpenseRepository _expenseRepository;
        private readonly ICashTransactionRepository _cashTransactionRepository;
        private readonly IBankTransactionRepository _bankTransactionRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly ICustomerLedgerRepository _customerLedgerRepository;
        private readonly ISupplierLedgerRepository _supplierLedgerRepository;

        public FinancialCoordinator(
            ISqliteConnectionFactory connectionFactory,
            IReceiptRepository receiptRepository,
            IPaymentRepository paymentRepository,
            IExpenseRepository expenseRepository,
            ICashTransactionRepository cashTransactionRepository,
            IBankTransactionRepository bankTransactionRepository,
            ICustomerRepository customerRepository,
            ISupplierRepository supplierRepository,
            ICustomerLedgerRepository customerLedgerRepository,
            ISupplierLedgerRepository supplierLedgerRepository)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _receiptRepository = receiptRepository ?? throw new ArgumentNullException(nameof(receiptRepository));
            _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
            _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
            _cashTransactionRepository = cashTransactionRepository ?? throw new ArgumentNullException(nameof(cashTransactionRepository));
            _bankTransactionRepository = bankTransactionRepository ?? throw new ArgumentNullException(nameof(bankTransactionRepository));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _customerLedgerRepository = customerLedgerRepository ?? throw new ArgumentNullException(nameof(customerLedgerRepository));
            _supplierLedgerRepository = supplierLedgerRepository ?? throw new ArgumentNullException(nameof(supplierLedgerRepository));
        }

        public async Task<int> PostReceiptAsync(Receipt receipt, CancellationToken cancellationToken = default)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
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
                // 1. Save Receipt within transaction
                var receiptId = await _receiptRepository.AddAsync(receipt, transaction, cancellationToken).ConfigureAwait(false);

                // 2. Fetch Customer and decrement OutstandingBalance
                var customer = await _customerRepository.GetByIdAsync(receipt.CustomerId, transaction, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Customer with ID {receipt.CustomerId} not found.");

                var newCustBalance = customer.OutstandingBalance - receipt.Amount;
                var custBalanceUpdated = await _customerRepository.UpdateBalanceAsync(receipt.CustomerId, newCustBalance, transaction, cancellationToken).ConfigureAwait(false);
                if (!custBalanceUpdated)
                {
                    throw new InvalidOperationException($"Failed to update outstanding balance for Customer ID {receipt.CustomerId}.");
                }

                // 3. Add CustomerLedger entry (Credit reduces customer receivable)
                var ledgerEntry = new CustomerLedgerEntry
                {
                    CustomerId = receipt.CustomerId,
                    TransactionDate = receipt.ReceiptDate == default ? DateTime.UtcNow : receipt.ReceiptDate,
                    TransactionType = "RECEIPT",
                    ReferenceNumber = receipt.ReceiptNumber,
                    Description = string.IsNullOrWhiteSpace(receipt.Remarks) ? $"Receipt {receipt.ReceiptNumber}" : receipt.Remarks,
                    Debit = 0.0m,
                    Credit = receipt.Amount,
                    RunningBalance = newCustBalance
                };
                await _customerLedgerRepository.AddAsync(ledgerEntry, transaction, cancellationToken).ConfigureAwait(false);

                // 4. Update Cash or Bank ledger depending on PaymentMode
                var isCash = string.Equals(receipt.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase);
                if (isCash)
                {
                    var currentCash = await _cashTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newCashBalance = currentCash + receipt.Amount;

                    var cashTx = new CashTransaction
                    {
                        TransactionDate = receipt.ReceiptDate == default ? DateTime.UtcNow : receipt.ReceiptDate,
                        TransactionType = "RECEIPT",
                        ReferenceNumber = receipt.ReceiptNumber,
                        Description = string.IsNullOrWhiteSpace(receipt.Remarks) ? $"Customer Receipt {receipt.ReceiptNumber}" : receipt.Remarks,
                        Debit = receipt.Amount,
                        Credit = 0.0m,
                        RunningBalance = newCashBalance,
                        AdminId = receipt.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cashTransactionRepository.AddAsync(cashTx, transaction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var currentBank = await _bankTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newBankBalance = currentBank + receipt.Amount;

                    var bankTx = new BankTransaction
                    {
                        TransactionDate = receipt.ReceiptDate == default ? DateTime.UtcNow : receipt.ReceiptDate,
                        TransactionType = "RECEIPT",
                        ReferenceNumber = receipt.ReceiptNumber,
                        Description = string.IsNullOrWhiteSpace(receipt.Remarks) ? $"Customer Receipt {receipt.ReceiptNumber}" : receipt.Remarks,
                        Debit = receipt.Amount,
                        Credit = 0.0m,
                        RunningBalance = newBankBalance,
                        AdminId = receipt.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _bankTransactionRepository.AddAsync(bankTx, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
                    VALUES (@AdminId, 'CREATE', 'Receipts', @Reference, @NewValue);",
                    new { receipt.AdminId, Reference = receipt.ReceiptNumber, NewValue = $"CustomerId={receipt.CustomerId}; Amount={receipt.Amount}" },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                // 5. Commit
                transaction.Commit();
                return receiptId;
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

        public async Task<int> PostPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            if (payment == null)
            {
                throw new ArgumentNullException(nameof(payment));
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
                // 1. Save Payment within transaction
                var paymentId = await _paymentRepository.AddAsync(payment, transaction, cancellationToken).ConfigureAwait(false);

                // 2. Fetch Supplier and decrement OutstandingBalance
                var supplier = await _supplierRepository.GetByIdAsync(payment.SupplierId, transaction, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Supplier with ID {payment.SupplierId} not found.");

                var newSuppBalance = supplier.OutstandingBalance - payment.Amount;
                var suppBalanceUpdated = await _supplierRepository.UpdateBalanceAsync(payment.SupplierId, newSuppBalance, transaction, cancellationToken).ConfigureAwait(false);
                if (!suppBalanceUpdated)
                {
                    throw new InvalidOperationException($"Failed to update outstanding balance for Supplier ID {payment.SupplierId}.");
                }

                // 3. Add SupplierLedger entry (Debit decreases supplier payable)
                var ledgerEntry = new SupplierLedgerEntry
                {
                    SupplierId = payment.SupplierId,
                    TransactionDate = payment.PaymentDate == default ? DateTime.UtcNow : payment.PaymentDate,
                    TransactionType = "PAYMENT",
                    ReferenceNumber = payment.PaymentNumber,
                    Description = string.IsNullOrWhiteSpace(payment.Remarks) ? $"Payment {payment.PaymentNumber}" : payment.Remarks,
                    Debit = payment.Amount,
                    Credit = 0.0m,
                    RunningBalance = newSuppBalance
                };
                await _supplierLedgerRepository.AddAsync(ledgerEntry, transaction, cancellationToken).ConfigureAwait(false);

                // 4. Update Cash or Bank ledger depending on PaymentMode
                var isCash = string.Equals(payment.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase);
                if (isCash)
                {
                    var currentCash = await _cashTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newCashBalance = currentCash - payment.Amount;

                    var cashTx = new CashTransaction
                    {
                        TransactionDate = payment.PaymentDate == default ? DateTime.UtcNow : payment.PaymentDate,
                        TransactionType = "PAYMENT",
                        ReferenceNumber = payment.PaymentNumber,
                        Description = string.IsNullOrWhiteSpace(payment.Remarks) ? $"Supplier Payment {payment.PaymentNumber}" : payment.Remarks,
                        Debit = 0.0m,
                        Credit = payment.Amount,
                        RunningBalance = newCashBalance,
                        AdminId = payment.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cashTransactionRepository.AddAsync(cashTx, transaction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var currentBank = await _bankTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newBankBalance = currentBank - payment.Amount;

                    var bankTx = new BankTransaction
                    {
                        TransactionDate = payment.PaymentDate == default ? DateTime.UtcNow : payment.PaymentDate,
                        TransactionType = "PAYMENT",
                        ReferenceNumber = payment.PaymentNumber,
                        Description = string.IsNullOrWhiteSpace(payment.Remarks) ? $"Supplier Payment {payment.PaymentNumber}" : payment.Remarks,
                        Debit = 0.0m,
                        Credit = payment.Amount,
                        RunningBalance = newBankBalance,
                        AdminId = payment.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _bankTransactionRepository.AddAsync(bankTx, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
                    VALUES (@AdminId, 'CREATE', 'Payments', @Reference, @NewValue);",
                    new { payment.AdminId, Reference = payment.PaymentNumber, NewValue = $"SupplierId={payment.SupplierId}; Amount={payment.Amount}" },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                // 5. Commit
                transaction.Commit();
                return paymentId;
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

        public async Task<int> PostExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
        {
            if (expense == null)
            {
                throw new ArgumentNullException(nameof(expense));
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
                // 1. Save Expense within transaction
                var expenseId = await _expenseRepository.AddAsync(expense, transaction, cancellationToken).ConfigureAwait(false);

                // 2. Update Cash or Bank ledger depending on PaymentSource
                var isCash = string.Equals(expense.PaymentSource, "Cash", StringComparison.OrdinalIgnoreCase);
                if (isCash)
                {
                    var currentCash = await _cashTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newCashBalance = currentCash - expense.Amount;

                    var cashTx = new CashTransaction
                    {
                        TransactionDate = expense.ExpenseDate == default ? DateTime.UtcNow : expense.ExpenseDate,
                        TransactionType = "EXPENSE",
                        ReferenceNumber = $"EXP-{expenseId}",
                        Description = string.IsNullOrWhiteSpace(expense.Description) ? $"Expense {expense.ExpenseCategory}" : expense.Description,
                        Debit = 0.0m,
                        Credit = expense.Amount,
                        RunningBalance = newCashBalance,
                        AdminId = expense.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cashTransactionRepository.AddAsync(cashTx, transaction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var currentBank = await _bankTransactionRepository.GetCurrentBalanceAsync(transaction, cancellationToken).ConfigureAwait(false);
                    var newBankBalance = currentBank - expense.Amount;

                    var bankTx = new BankTransaction
                    {
                        TransactionDate = expense.ExpenseDate == default ? DateTime.UtcNow : expense.ExpenseDate,
                        TransactionType = "EXPENSE",
                        ReferenceNumber = $"EXP-{expenseId}",
                        Description = string.IsNullOrWhiteSpace(expense.Description) ? $"Expense {expense.ExpenseCategory}" : expense.Description,
                        Debit = 0.0m,
                        Credit = expense.Amount,
                        RunningBalance = newBankBalance,
                        AdminId = expense.AdminId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _bankTransactionRepository.AddAsync(bankTx, transaction, cancellationToken).ConfigureAwait(false);
                }

                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
                    VALUES (@AdminId, 'CREATE', 'Expenses', @Reference, @NewValue);",
                    new { expense.AdminId, Reference = $"EXP-{expenseId}", NewValue = $"Category={expense.ExpenseCategory}; Amount={expense.Amount}" },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                // 3. Commit
                transaction.Commit();
                return expenseId;
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
    }
}
