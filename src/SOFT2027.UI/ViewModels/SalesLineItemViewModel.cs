using System;
using SOFT2027.Core.Entities;

namespace SOFT2027.Presentation.ViewModels
{
    public class SalesLineItemViewModel : ViewModelBase
    {
        private Product? _product;
        private decimal _quantity = 1m;
        private decimal _unitPrice;
        private decimal _costPrice;
        private decimal _sellingPercentage;
        private decimal _discountPercentage;
        private decimal _lineTotal;
        private string? _pricingSource; // "Tier 1 Default" or "Tier 2 Customer Memory"

        public Product? Product
        {
            get => _product;
            set => SetProperty(ref _product, value, RecalculateLineTotal);
        }

        public int ProductId => Product?.ProductId ?? 0;
        public string ProductName => Product?.ProductName ?? string.Empty;

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value, RecalculateLineTotal);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value, RecalculateLineTotal);
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set => SetProperty(ref _costPrice, value);
        }

        public decimal SellingPercentage
        {
            get => _sellingPercentage;
            set => SetProperty(ref _sellingPercentage, value);
        }

        public decimal DiscountPercentage
        {
            get => _discountPercentage;
            set => SetProperty(ref _discountPercentage, value, RecalculateLineTotal);
        }

        public decimal LineTotal
        {
            get => _lineTotal;
            set => SetProperty(ref _lineTotal, value);
        }

        public string? PricingSource
        {
            get => _pricingSource;
            set => SetProperty(ref _pricingSource, value);
        }

        public event Action? LineTotalChanged;

        public void RecalculateLineTotal()
        {
            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercentage / 100m);
            LineTotal = gross - discount;
            LineTotalChanged?.Invoke();
        }
    }
}
