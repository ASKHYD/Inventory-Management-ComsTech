using System;

namespace SOFT2027.Core.Entities
{
    public class Receipt
    {
        public int ReceiptId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMode { get; set; } = "Cash"; // Cash, Cheque, OnlineTransfer
        public string? ReferenceDetails { get; set; }
        public int AdminId { get; set; }
        public string? Remarks { get; set; }
    }
}
