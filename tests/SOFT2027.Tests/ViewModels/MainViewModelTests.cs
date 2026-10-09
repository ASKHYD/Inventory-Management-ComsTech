using System;
using FluentAssertions;
using Moq;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.ViewModels;
using Xunit;

namespace SOFT2027.Tests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<ITransactionCoordinator> _mockCoordinator = new();
        private readonly Mock<ICustomerRepository> _mockCustomerRepo = new();
        private readonly Mock<IProductRepository> _mockProductRepo = new();
        private readonly Mock<ICustomerPricingHistoryRepository> _mockPricingRepo = new();
        private readonly Mock<IReportRepository> _mockReportRepo = new();

        private SalesEntryViewModel CreateSalesVm() =>
            new(_mockCoordinator.Object, _mockCustomerRepo.Object, _mockProductRepo.Object, _mockPricingRepo.Object);

        private ReportsDashboardViewModel CreateReportsVm() =>
            new(_mockReportRepo.Object);

        [Fact]
        public void NavigateToSalesCommand_ShouldSetCurrentViewModelToSalesEntry()
        {
            var salesVm = CreateSalesVm();
            var reportsVm = CreateReportsVm();
            var sut = new MainViewModel(() => salesVm, () => reportsVm);

            sut.NavigateToSalesCommand.Execute(null);

            sut.CurrentViewModel.Should().BeSameAs(salesVm);
            sut.StatusAnnouncement.Should().Contain("Sales Entry");
        }

        [Fact]
        public void NavigateToReportsCommand_ShouldSetCurrentViewModelToReportsDashboard()
        {
            var salesVm = CreateSalesVm();
            var reportsVm = CreateReportsVm();
            var sut = new MainViewModel(() => salesVm, () => reportsVm);

            sut.NavigateToReportsCommand.Execute(null);

            sut.CurrentViewModel.Should().BeSameAs(reportsVm);
            sut.StatusAnnouncement.Should().Contain("Reports Dashboard");
        }
    }
}
