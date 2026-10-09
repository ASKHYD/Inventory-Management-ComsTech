using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities
{
    public class CustomerStatement
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<CustomerStatementItem> Transactions { get; set; } = new();
    }

    public class CustomerStatementItem
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty; // OPENING, SALE, RECEIPT, SALE_RETURN, etc.
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }
}
