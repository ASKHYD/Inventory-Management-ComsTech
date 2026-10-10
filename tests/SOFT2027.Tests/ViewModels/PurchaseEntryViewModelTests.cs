using System;
using FluentAssertions;
using Moq;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.ViewModels;
using Xunit;

namespace SOFT2027.Tests.ViewModels;

public sealed class PurchaseEntryViewModelTests
{
    private readonly Mock<ITransactionCoordinator> _transactions = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ISupplierPricingHistoryRepository> _pricing = new();

    private PurchaseEntryViewModel CreateSut() => new(_transactions.Object, _suppliers.Object, _products.Object, _pricing.Object);

    [Fact]
    public void NewSupplierProductPair_UsesCurrentDefaultAndNoPreviousOption()
    {
        _pricing.Setup(repository => repository.GetLatestAsync(7, 11, default)).ReturnsAsync((SupplierProductPricingHistory?)null);
        var sut = CreateSut();
        sut.SelectedProduct = new Product { ProductId = 11, ProductName = "Tea", BasePurchasePrice = 80m, PurchasePercentage = 5m };
        sut.SelectedSupplier = new Supplier { SupplierId = 7, SupplierName = "Supplier" };

        sut.UnitPrice.Should().Be(80m);
        sut.PurchasePercentage.Should().Be(5m);
        sut.DiscountPercentage.Should().Be(0m);
        sut.PricingMessage.Should().Contain("No earlier purchase");
        sut.UsePreviousPriceCommand.CanExecute(null).Should().BeFalse();
        sut.UseDefaultPriceCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ExistingSupplierProductPair_RestoresAllPreviousTermsAndDefaultClearsDiscount()
    {
        var previous = new SupplierProductPricingHistory
        {
            SupplierId = 7, ProductId = 11, PurchasePrice = 72m, PurchasePercentage = 3m,
            DiscountPercentage = 10m, Quantity = 12m, InvoiceNumber = "PUR-OLD", TransactionDate = new DateTime(2026, 4, 2)
        };
        _pricing.Setup(repository => repository.GetLatestAsync(7, 11, default)).ReturnsAsync(previous);
        var sut = CreateSut();
        sut.SelectedProduct = new Product { ProductId = 11, ProductName = "Tea", BasePurchasePrice = 80m, PurchasePercentage = 5m };
        sut.SelectedSupplier = new Supplier { SupplierId = 7, SupplierName = "Supplier" };

        sut.UnitPrice.Should().Be(72m);
        sut.PurchasePercentage.Should().Be(3m);
        sut.DiscountPercentage.Should().Be(10m);
        sut.PricingMessage.Should().Contain("PUR-OLD").And.Contain("10.00%");

        sut.UseDefaultPriceCommand.Execute(null);
        sut.UnitPrice.Should().Be(80m);
        sut.PurchasePercentage.Should().Be(5m);
        sut.DiscountPercentage.Should().Be(0m);

        sut.UsePreviousPriceCommand.Execute(null);
        sut.UnitPrice.Should().Be(72m);
        sut.PurchasePercentage.Should().Be(3m);
        sut.DiscountPercentage.Should().Be(10m);

        sut.SelectedProduct!.BasePurchasePrice = 85m;
        sut.UseDefaultPriceCommand.Execute(null);
        sut.UnitPrice.Should().Be(85m);
        sut.DiscountPercentage.Should().Be(0m);
        sut.EnterNewPriceCommand.Execute(null);
        sut.UnitPrice = 70m;
        sut.DiscountPercentage = 2m;
        sut.Quantity = 2m;
        sut.AddItemCommand.Execute(null);
        sut.Items.Should().ContainSingle().Which.UnitPrice.Should().Be(70m);
        sut.Items.Single().DiscountPercentage.Should().Be(2m);
        sut.Items.Single().LineTotal.Should().Be(137.20m);
    }
}
