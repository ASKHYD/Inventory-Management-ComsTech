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

namespace SOFT2027.Tests.ViewModels;

public class MasterDataViewModelTests
{
    private readonly Mock<IProductRepository> _mockProductRepo = new();
    private readonly Mock<ICustomerRepository> _mockCustomerRepo = new();
    private readonly Mock<ISupplierRepository> _mockSupplierRepo = new();
    private readonly Mock<IBusinessLookupRepository> _mockLookupRepo = new();
    private readonly Mock<IAuditLogRepository> _mockAuditRepo = new();
    private readonly Mock<IAdminSession> _mockSession = new();

    private MasterDataViewModel CreateSut(string section) =>
        new(section, _mockProductRepo.Object, _mockCustomerRepo.Object, _mockSupplierRepo.Object,
            _mockLookupRepo.Object, _mockAuditRepo.Object, _mockSession.Object);

    [Fact]
    public void AddCommand_ShouldOpenForm_AndInitializeDraft()
    {
        var sut = CreateSut("Products");

        sut.AddCommand.Execute(null);

        sut.IsAddFormOpen.Should().BeTrue();
        sut.AddFormTitle.Should().Be("Add New Product");
        sut.NewRecordDraft.Should().NotBeNull();
        sut.NewRecordDraft.Should().BeOfType<Product>();
        var product = (Product)sut.NewRecordDraft!;
        product.ProductCode.Should().StartWith("P-");
    }

    [Fact]
    public void CancelAddCommand_ShouldCloseForm_AndClearDraft()
    {
        var sut = CreateSut("Customers");
        sut.AddCommand.Execute(null);
        sut.IsAddFormOpen.Should().BeTrue();

        sut.CancelAddCommand.Execute(null);

        sut.IsAddFormOpen.Should().BeFalse();
        sut.NewRecordDraft.Should().BeNull();
    }

    [Fact]
    public async Task SaveNewRecordAsync_ShouldFailValidation_WhenProductNameMissing()
    {
        var sut = CreateSut("Products");
        sut.AddCommand.Execute(null);
        var product = (Product)sut.NewRecordDraft!;
        product.ProductName = "   "; // Empty name

        await sut.SaveNewRecordCommand.ExecuteAsync(null);

        sut.HasErrors.Should().BeTrue();
        sut.ErrorMessage.Should().Contain("Product Name is required");
        sut.IsAddFormOpen.Should().BeTrue(); // Form stays open
        _mockProductRepo.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveNewRecordAsync_ShouldPersist_AndCloseForm_WhenValid()
    {
        _mockProductRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());
        _mockLookupRepo.Setup(r => r.GetAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<LookupItem>());

        var sut = CreateSut("Products");
        sut.AddCommand.Execute(null);
        var product = (Product)sut.NewRecordDraft!;
        product.ProductName = "Super Basmati Rice 50kg";
        product.BasePurchasePrice = 5000;
        product.DefaultSalePrice = 5500;
        product.MinimumStock = 10;

        await sut.SaveNewRecordCommand.ExecuteAsync(null);

        sut.HasErrors.Should().BeFalse();
        sut.IsAddFormOpen.Should().BeFalse(); // Form closed on success
        sut.StatusAnnouncement.Should().Contain("added successfully");
        _mockProductRepo.Verify(r => r.AddAsync(It.Is<Product>(p => p.ProductName == "Super Basmati Rice 50kg"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OpenEditForm_ShouldPrepopulateDraft_AndSetEditTitle()
    {
        var sut = CreateSut("Products");
        var existingProduct = new Product
        {
            ProductId = 42,
            ProductCode = "P-TEST-01",
            ProductName = "Original Name",
            BasePurchasePrice = 1200,
            DefaultSalePrice = 1500,
            MinimumStock = 5,
            CurrentStock = 20,
            Status = "Active"
        };

        sut.OpenEditForm(existingProduct);

        sut.IsAddFormOpen.Should().BeTrue();
        sut.AddFormTitle.Should().Be("Edit Product: Original Name");
        sut.SaveNewActionLabel.Should().Be("Update Product");
        sut.NewRecordDraft.Should().NotBeNull();
        sut.NewRecordDraft.Should().BeOfType<Product>();
        var draft = (Product)sut.NewRecordDraft!;
        draft.ProductId.Should().Be(42);
        draft.ProductName.Should().Be("Original Name");
        draft.BasePurchasePrice.Should().Be(1200);
    }

    [Fact]
    public async Task SaveNewRecordAsync_ShouldCallUpdate_WhenEditingExistingProduct()
    {
        _mockProductRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());
        _mockLookupRepo.Setup(r => r.GetAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<LookupItem>());

        var sut = CreateSut("Products");
        var existingProduct = new Product
        {
            ProductId = 10,
            ProductCode = "P-10",
            ProductName = "Old Name",
            BasePurchasePrice = 100,
            DefaultSalePrice = 150,
            MinimumStock = 2
        };

        sut.OpenEditForm(existingProduct);
        var draft = (Product)sut.NewRecordDraft!;
        draft.ProductName = "Updated Name";

        await sut.SaveNewRecordCommand.ExecuteAsync(null);

        sut.HasErrors.Should().BeFalse();
        sut.IsAddFormOpen.Should().BeFalse();
        sut.StatusAnnouncement.Should().Contain("updated successfully");
        _mockProductRepo.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.ProductId == 10 && p.ProductName == "Updated Name"), It.IsAny<CancellationToken>()), Times.Once);
        _mockProductRepo.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void StatusFilter_ShouldUpdateSelectedStatusFilter()
    {
        var sut = CreateSut("Products");
        sut.SelectedStatusFilter.Should().Be("All");
        sut.IsAllFilterSelected.Should().BeTrue();

        sut.SetStatusFilterCommand.Execute("Active");
        sut.SelectedStatusFilter.Should().Be("Active");
        sut.IsActiveFilterSelected.Should().BeTrue();
        sut.IsAllFilterSelected.Should().BeFalse();

        sut.SetStatusFilterCommand.Execute("Inactive");
        sut.SelectedStatusFilter.Should().Be("Inactive");
        sut.IsInactiveFilterSelected.Should().BeTrue();
    }
}
