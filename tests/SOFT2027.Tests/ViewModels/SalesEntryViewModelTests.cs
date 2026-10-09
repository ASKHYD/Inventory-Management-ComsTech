using System;
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
                DefaultSalePrice = 2500m
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
                DefaultSalePrice = 2500m
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

            sut.StatusAnnouncement.Should().Contain("saved successfully");
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
                Product = new Product { ProductId = 101, ProductName = "Basmati Rice 25kg" },
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
    }
}
