using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class InventoryViewModel : ViewModelBase
{
    private readonly IProductRepository _products;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryCoordinator _inventory;
    private readonly IAdminSession _session;
    private Product? _selectedProduct;
    private decimal _quantityChange;
    private string _reason = string.Empty;
    private bool _isBusy;
    private int _movementLoadVersion;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<StockMovement> MovementHistory { get; } = new();
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set { if (SetProperty(ref _selectedProduct, value)) _ = LoadMovementsAsync(); }
    }
    public decimal QuantityChange { get => _quantityChange; set => SetProperty(ref _quantityChange, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public int LowStockCount => Products.Count(product => product.CurrentStock <= product.MinimumStock);
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand AdjustCommand { get; }

    public InventoryViewModel(IProductRepository products, IStockMovementRepository movements, IInventoryCoordinator inventory, IAdminSession session)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AdjustCommand = new AsyncRelayCommand(AdjustAsync, () => !IsBusy);
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            Products.Clear();
            foreach (var product in await _products.GetAllAsync()) if (product.Status == "Active") Products.Add(product);
            OnPropertyChanged(nameof(LowStockCount));
            if (SelectedProduct != null) SelectedProduct = Products.FirstOrDefault(p => p.ProductId == SelectedProduct.ProductId);
            Announce("Current stock loaded.");
        }
        catch (Exception ex) { SetError($"Could not load inventory. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task LoadMovementsAsync()
    {
        var version = ++_movementLoadVersion;
        var selectedProduct = SelectedProduct;
        MovementHistory.Clear();
        if (selectedProduct == null) return;
        try
        {
            var movements = await _movements.GetByProductIdAsync(selectedProduct.ProductId);
            if (version != _movementLoadVersion || SelectedProduct?.ProductId != selectedProduct.ProductId) return;
            foreach (var movement in movements) MovementHistory.Add(movement);
        }
        catch (Exception ex) { SetError($"Could not load this product's stock history. {ex.Message}"); }
    }

    private async Task AdjustAsync()
    {
        if (SelectedProduct == null) { SetError("Select a product first."); return; }
        if (QuantityChange == 0) { SetError("Enter a positive number to add stock or a negative number to remove stock."); return; }
        if (string.IsNullOrWhiteSpace(Reason)) { SetError("Enter a short reason, such as damaged goods or opening stock."); return; }
        try
        {
            IsBusy = true;
            ClearErrors();
            var id = SelectedProduct.ProductId;
            await _inventory.AdjustStockAsync(id, QuantityChange, _session.AdminId, Reason);
            QuantityChange = 0;
            Reason = string.Empty;
            await LoadAsync();
            Announce("Stock adjustment saved. The stock movement and audit record were saved together.");
            SelectedProduct = Products.FirstOrDefault(product => product.ProductId == id);
        }
        catch (Exception ex) { SetError($"Stock was not changed. {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
