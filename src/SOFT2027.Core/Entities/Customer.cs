using System;

namespace SOFT2027.Core.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? AreaTerritory { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
