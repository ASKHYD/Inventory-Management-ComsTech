using System;

namespace SOFT2027.Core.Entities
{
    public class Expense
    {
        public int ExpenseId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string ExpenseCategory { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PaymentSource { get; set; } = "Cash"; // Cash, Bank
        public int AdminId { get; set; }
    }
}
