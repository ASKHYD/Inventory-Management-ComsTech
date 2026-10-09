using System;

namespace SOFT2027.Core.Entities
{
    public class BankTransaction
    {
        public int BankTransactionId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty; // RECEIPT, PAYMENT, EXPENSE, etc.
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public int AdminId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
