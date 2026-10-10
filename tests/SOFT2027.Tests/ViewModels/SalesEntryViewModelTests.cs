using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.ViewModels;
using Xunit;

namespace SOFT2027.Tests.ViewModels
{
    public class SalesEntryViewModelTests
    {
        private readonly Mock<ITransactionCoordinator> _mockCoordinator = new();
        private readonly Mock<ICustomerRepository> _mockCustomerRepo = new();
        private readonly Mock<IProductRepository> _mockProductRepo = new();
        private readonly Mock<ICustomerPricingHistoryRepository> _mockPricingRepo = new();

        private SalesEntryViewModel CreateSut() =>
            new(_mockCoordinator.Object, _mockCustomerRepo.Object, _mockProductRepo.Object, _mockPricingRepo.Object);

        [Fact]
        public void AddItemCommand_ShouldAddLineItem_AndRecalculateTotals()
        {
            var sut = CreateSut();
            sut.SelectedProduct = new Product
            {
                ProductId = 101,
                ProductName = "Basmati Rice 25kg",
                BasePurchasePrice = 2000m,
                DefaultSalePrice = 2500m,
                CurrentStock = 100m
            };
            sut.Quantity = 2m;
            sut.UnitPrice = 2500m;
            sut.DiscountPercentage = 10m;

            sut.AddItemCommand.Execute(null);

            sut.Items.Should().HaveCount(1);
            sut.SubTotal.Should().Be(5000m);
            sut.DiscountAmount.Should().Be(500m);
            sut.NetTotal.Should().Be(4500m);
            sut.RemainingBalance.Should().Be(4500m);
            sut.StatusAnnouncement.Should().Contain("Basmati Rice 25kg added");
            sut.HasErrors.Should().BeFalse();
        }

        [Fact]
        public async Task SaveSaleCommand_ShouldInvokeTransactionCoordinator_WhenValid()
        {
            var sut = CreateSut();
            sut.SelectedCustomer = new Customer
            {
                CustomerId = 1,
                CustomerName = "Al-Madina Traders",
                OutstandingBalance = 1000m
            };

            var product = new Product
            {
                ProductId = 101,
                ProductName = "Basmati Rice 25kg",
                BasePurchasePrice = 2000m,
                DefaultSalePrice = 2500m,
                CurrentStock = 100m
            };

            sut.Items.Add(new SalesLineItemViewModel
            {
                Product = product,
                Quantity = 2m,
                UnitPrice = 2500m,
                CostPrice = 2000m,
                DiscountPercentage = 0m,
                LineTotal = 5000m
            });

            sut.SubTotal = 5000m;
            sut.DiscountAmount = 0m;
            sut.NetTotal = 5000m;
            sut.PaidAmount = 2000m;
            sut.RemainingBalance = 3000m;

            _mockCoordinator
                .Setup(c => c.PostSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            await sut.SaveSaleCommand.ExecuteAsync(null);

            _mockCoordinator.Verify(c => c.PostSaleAsync(
                It.Is<Sale>(s => s.CustomerId == 1 && s.NetTotal == 5000m && s.PaidAmount == 2000m),
                It.IsAny<CancellationToken>()), Times.Once);

            sut.StatusAnnouncement.Should().Contain("saved.");
            sut.HasErrors.Should().BeFalse();
            sut.ErrorMessage.Should().BeNull();
            sut.IsBusy.Should().BeFalse();
        }

        [Fact]
        public async Task SaveSaleCommand_ShouldSetErrorMessageAndStatusAnnouncement_WhenValidationFails()
        {
            var sut = CreateSut();
            sut.SelectedCustomer = null; // Validation failure: No customer selected
            sut.Items.Clear();           // Validation failure: No line items

            await sut.SaveSaleCommand.ExecuteAsync(null);

            _mockCoordinator.Verify(c => c.PostSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()), Times.Never);
            sut.HasErrors.Should().BeTrue();
            sut.ErrorMessage.Should().NotBeNullOrWhiteSpace();
            sut.StatusAnnouncement.Should().Contain("Validation Error");
            sut.IsBusy.Should().BeFalse();
        }

        [Fact]
        public async Task SaveSaleCommand_ShouldSetErrorMessageAndStatusAnnouncement_WhenCoordinatorThrows()
        {
            var sut = CreateSut();
            sut.SelectedCustomer = new Customer
            {
                CustomerId = 1,
                CustomerName = "Al-Madina Traders"
            };

            sut.Items.Add(new SalesLineItemViewModel
            {
                Product = new Product { ProductId = 101, ProductName = "Basmati Rice 25kg", CurrentStock = 100m },
                Quantity = 10m,
                UnitPrice = 2500m,
                LineTotal = 25000m
            });

            sut.SubTotal = 25000m;
            sut.NetTotal = 25000m;

            _mockCoordinator
                .Setup(c => c.PostSaleAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Insufficient stock in warehouse"));

            await sut.SaveSaleCommand.ExecuteAsync(null);

            sut.HasErrors.Should().BeTrue();
            sut.ErrorMessage.Should().Contain("Insufficient stock in warehouse");
            sut.StatusAnnouncement.Should().Contain("Error");
            sut.IsBusy.Should().BeFalse();
        }

        [Fact]
        public void CustomerProductPricing_UsesDefaultForFirstPairAndPreviousTermsWhenAvailable()
        {
            var previous = new CustomerProductPricingHistory
            {
                CustomerId = 2, ProductId = 101, SalePrice = 120m, SellingPercentage = 20m,
                DiscountPercentage = 5m, Quantity = 4m, InvoiceNumber = "INV-OLD", TransactionDate = new DateTime(2026, 4, 2)
            };
            _mockPricingRepo.Setup(repository => repository.GetLatestAsync(2, 101, It.IsAny<CancellationToken>())).ReturnsAsync(previous);
            var sut = CreateSut();
            sut.SelectedProduct = new Product { ProductId = 101, ProductName = "Tea", BasePurchasePrice = 100m, DefaultSalePrice = 150m };
            sut.SelectedCustomer = new Customer { CustomerId = 2, CustomerName = "Shop" };

            sut.UnitPrice.Should().Be(120m);
            sut.DiscountPercentage.Should().Be(5m);
            sut.PricingMessage.Should().Contain("INV-OLD").And.Contain("5.00%");
            sut.UsePreviousPriceCommand.CanExecute(null).Should().BeTrue();

            sut.UseDefaultPriceCommand.Execute(null);
            sut.UnitPrice.Should().Be(150m);
            sut.DiscountPercentage.Should().Be(0m);
            sut.SelectedProduct!.DefaultSalePrice = 160m;
            sut.UseDefaultPriceCommand.Execute(null);
            sut.UnitPrice.Should().Be(160m);
            sut.EnterNewPriceCommand.Execute(null);
            sut.UnitPrice = 135m;
            sut.DiscountPercentage = 2m;
            sut.Quantity = 2m;
            sut.AddItemCommand.Execute(null);
            sut.Items.Should().ContainSingle().Which.UnitPrice.Should().Be(135m);
            sut.Items.Single().DiscountPercentage.Should().Be(2m);
            sut.Items.Single().LineTotal.Should().Be(264.60m);
        }

        [Fact]
        public void FirstTimeCustomerProductPair_UsesDefaultWithoutOfferingPreviousPrice()
        {
            _mockPricingRepo.Setup(repository => repository.GetLatestAsync(3, 102, It.IsAny<CancellationToken>())).ReturnsAsync((CustomerProductPricingHistory?)null);
            var sut = CreateSut();
            sut.SelectedProduct = new Product { ProductId = 102, ProductName = "Soap", BasePurchasePrice = 50m, DefaultSalePrice = 60m };
            sut.SelectedCustomer = new Customer { CustomerId = 3, CustomerName = "New shop" };

            sut.UnitPrice.Should().Be(60m);
            sut.DiscountPercentage.Should().Be(0m);
            sut.PricingMessage.Should().Contain("No earlier sale");
            sut.UsePreviousPriceCommand.CanExecute(null).Should().BeFalse();
            sut.UseDefaultPriceCommand.CanExecute(null).Should().BeTrue();
        }
    }
}
