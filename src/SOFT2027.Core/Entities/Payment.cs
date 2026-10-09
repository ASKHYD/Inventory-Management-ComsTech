using System;

namespace SOFT2027.Core.Entities
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMode { get; set; } = "Cash"; // Cash, Cheque, OnlineTransfer
        public string? ReferenceDetails { get; set; }
        public int AdminId { get; set; }
        public string? Remarks { get; set; }
    }
}
