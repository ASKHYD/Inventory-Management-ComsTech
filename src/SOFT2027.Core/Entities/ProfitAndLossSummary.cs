using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities
{
    public class ProfitAndLossSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossMarginPercentage => TotalRevenue > 0 ? Math.Round((GrossProfit / TotalRevenue) * 100m, 2) : 0m;
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetMarginPercentage => TotalRevenue > 0 ? Math.Round((NetProfit / TotalRevenue) * 100m, 2) : 0m;
        public List<ExpenseCategorySummary> ExpensesByCategory { get; set; } = new();
    }

    public class ExpenseCategorySummary
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}
