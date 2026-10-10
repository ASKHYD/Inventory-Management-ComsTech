using System;
using System.Collections.Generic;
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
    public class ReportsDashboardViewModelTests
    {
        private readonly Mock<IReportRepository> _mockReportRepo = new();

        private ReportsDashboardViewModel CreateSut() =>
            new(_mockReportRepo.Object);

        [Fact]
        public async Task LoadReportsCommand_ShouldInvokeReportRepository_AndPopulateSummaries()
        {
            var sut = CreateSut();
            var pnl = new ProfitAndLossSummary
            {
                StartDate = sut.StartDate,
                EndDate = sut.EndDate,
                TotalRevenue = 50000m,
                CostOfGoodsSold = 35000m,
                GrossProfit = 15000m,
                TotalExpenses = 5000m,
                NetProfit = 10000m
            };

            var stockVal = new StockValuation
            {
                TotalValuation = 120000m,
                TotalPotentialRetailValue = 160000m,
                TotalItemsCount = 25
            };

            _mockReportRepo
                .Setup(r => r.GetProfitAndLossSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pnl);

            _mockReportRepo
                .Setup(r => r.GetStockValuationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockVal);

            _mockReportRepo
                .Setup(r => r.GetOperationalReportAsync(It.IsAny<OperationalReportRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationalReport());

            await sut.LoadReportsCommand.ExecuteAsync(null);

            _mockReportRepo.Verify(r => r.GetProfitAndLossSummaryAsync(sut.StartDate, sut.EndDate, It.IsAny<CancellationToken>()), Times.Once);
            _mockReportRepo.Verify(r => r.GetStockValuationAsync(It.IsAny<CancellationToken>()), Times.Once);

            sut.ProfitAndLoss.Should().BeSameAs(pnl);
            sut.StockValuation.Should().BeSameAs(stockVal);
            sut.PeriodSales.Should().Be(0m);
            sut.PeriodPurchases.Should().Be(0m);
            sut.LowStockItemCount.Should().Be(0);
            sut.DashboardRecentActivity.Should().ContainSingle().Which.Text.Should().Contain("No sales");
            _mockReportRepo.Verify(r => r.GetOperationalReportAsync(It.IsAny<OperationalReportRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(9));
            sut.StatusAnnouncement.Should().Contain("Reports loaded successfully");
            sut.IsLoading.Should().BeFalse();
            sut.HasErrors.Should().BeFalse();
            sut.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task LoadReportsCommand_ShouldSetErrorMessageAndStatusAnnouncement_WhenRepositoryThrows()
        {
            var sut = CreateSut();

            _mockReportRepo
                .Setup(r => r.GetProfitAndLossSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Database connection timed out"));

            await sut.LoadReportsCommand.ExecuteAsync(null);

            sut.HasErrors.Should().BeTrue();
            sut.ErrorMessage.Should().Contain("Database connection timed out");
            sut.StatusAnnouncement.Should().Contain("Error");
            sut.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task LoadCustomerStatementCommand_ShouldInvokeRepository_WhenCustomerIdSelected()
        {
            var sut = CreateSut();
            sut.SelectedCustomerId = 15;

            var statement = new CustomerStatement
            {
                CustomerId = 15,
                CustomerName = "Target Store",
                OpeningBalance = 5000m,
                TotalDebits = 10000m,
                TotalCredits = 8000m,
                ClosingBalance = 7000m
            };

            _mockReportRepo
                .Setup(r => r.GetCustomerStatementAsync(15, sut.StartDate, sut.EndDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(statement);

            await sut.LoadCustomerStatementCommand.ExecuteAsync(null);

            _mockReportRepo.Verify(r => r.GetCustomerStatementAsync(15, sut.StartDate, sut.EndDate, It.IsAny<CancellationToken>()), Times.Once);
            sut.CustomerStatement.Should().BeSameAs(statement);
            sut.StatusAnnouncement.Should().Contain("Customer statement loaded");
            sut.HasErrors.Should().BeFalse();
            sut.ErrorMessage.Should().BeNull();
            sut.IsLoading.Should().BeFalse();
        }
    }
}
