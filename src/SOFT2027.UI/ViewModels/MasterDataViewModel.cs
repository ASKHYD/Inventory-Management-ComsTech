using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Data;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

/// <summary>Shared, searchable editor for the product, customer, and supplier lists.</summary>
public sealed class MasterDataViewModel : ViewModelBase
{
    private readonly string _section;
    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly IBusinessLookupRepository? _lookups;
    private string _searchText = string.Empty;
    private object? _selectedRecord;
    private bool _isBusy;

    public ObservableCollection<object> Records { get; } = new();
    public ObservableCollection<LookupItem> Categories { get; } = new();
    public ObservableCollection<LookupItem> Brands { get; } = new();
    public ObservableCollection<LookupItem> Units { get; } = new();
    public ICollectionView FilteredRecords { get; }
    public string Title => _section;
    public string AddLabel => $"Add {_section.TrimEnd('s')}";
    public string HelpText => _section switch
    {
        "Products" => "Keep product names and default prices here. Stock changes through purchases and stock adjustments.",
        "Customers" => "Keep customer contact details and opening balances here. Outstanding balances update from invoices and receipts.",
        _ => "Keep supplier contact details and opening balances here. Outstanding balances update from purchases and payments."
    };

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) FilteredRecords.Refresh();
        }
    }

    public object? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                OnPropertyChanged(nameof(StatusActionLabel));
                ToggleStatusCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusActionLabel => SelectedRecord?.GetType().GetProperty("Status")?.GetValue(SelectedRecord)?.ToString() == "Inactive"
        ? "Reactivate selected"
        : "Deactivate selected";

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand ToggleStatusCommand { get; }

    public MasterDataViewModel(string section, IProductRepository products, ICustomerRepository customers, ISupplierRepository suppliers, IBusinessLookupRepository? lookups = null)
    {
        _section = section is "Products" or "Customers" or "Suppliers"
            ? section
            : throw new ArgumentOutOfRangeException(nameof(section));
        _products = products;
        _customers = customers;
        _suppliers = suppliers;
        _lookups = lookups;
        FilteredRecords = CollectionViewSource.GetDefaultView(Records);
        FilteredRecords.Filter = MatchesSearch;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        AddCommand = new RelayCommand(AddRecord);
        ToggleStatusCommand = new RelayCommand(ToggleStatus, _ => SelectedRecord != null);
    }

    private bool MatchesSearch(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.Name is not "CreatedAt" and not "UpdatedAt")
            .Select(p => p.GetValue(item)?.ToString())
            .Any(value => value?.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) == true);
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            Records.Clear();
            if (_section == "Products")
            {
                if (_lookups != null)
                {
                    Categories.Clear(); Brands.Clear(); Units.Clear();
                    foreach (var item in await _lookups.GetAllAsync("Categories")) Categories.Add(item);
                    foreach (var item in await _lookups.GetAllAsync("Brands")) Brands.Add(item);
                    foreach (var item in await _lookups.GetAllAsync("Units")) Units.Add(item);
                }
                foreach (var row in await _products.GetAllAsync()) Records.Add(row);
            }
            else if (_section == "Customers")
                foreach (var row in await _customers.GetAllAsync()) Records.Add(row);
            else
                foreach (var row in await _suppliers.GetAllAsync()) Records.Add(row);
            Announce($"{_section} loaded.");
        }
        catch (Exception ex) { SetError($"Could not load {_section.ToLowerInvariant()}. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private void AddRecord()
    {
        SelectedRecord = _section switch
        {
            "Products" => new Product { ProductCode = $"P-{DateTime.Now:yyMMddHHmmss}", ProductName = "New product", Status = "Active" },
            "Customers" => new Customer { CustomerCode = $"C-{DateTime.Now:yyMMddHHmmss}", CustomerName = "New customer", Status = "Active" },
            _ => new Supplier { SupplierCode = $"S-{DateTime.Now:yyMMddHHmmss}", SupplierName = "New supplier", Status = "Active" }
        };
        Records.Add(SelectedRecord);
        Announce($"New {_section.TrimEnd('s').ToLowerInvariant()} row added. Enter its details, then choose Save Changes.");
    }

    private void ToggleStatus(object? _)
    {
        if (SelectedRecord == null) return;
        var status = SelectedRecord.GetType().GetProperty("Status");
        if (status == null) return;
        status.SetValue(SelectedRecord, Equals(status.GetValue(SelectedRecord), "Active") ? "Inactive" : "Active");
        FilteredRecords.Refresh();
        Announce("Status changed. Choose Save Changes to keep it.");
    }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            foreach (var row in Records.ToArray())
            {
                if (_section == "Products" && row is Product product)
                {
                    if (string.IsNullOrWhiteSpace(product.ProductCode) || string.IsNullOrWhiteSpace(product.ProductName))
                        throw new InvalidOperationException("Each product needs a code and a name.");
                    if (product.BasePurchasePrice < 0 || product.DefaultSalePrice < 0 || product.MinimumStock < 0)
                        throw new InvalidOperationException("Prices and minimum stock cannot be negative.");
                    if (product.ProductId == 0) await _products.AddAsync(product);
                    else await _products.UpdateAsync(product);
                }
                else if (_section == "Customers" && row is Customer customer)
                {
                    if (string.IsNullOrWhiteSpace(customer.CustomerCode) || string.IsNullOrWhiteSpace(customer.CustomerName))
                        throw new InvalidOperationException("Each customer needs a code and a name.");
                    if (customer.OpeningBalance < 0 || customer.CreditLimit < 0)
                        throw new InvalidOperationException("Opening balance and credit limit cannot be negative.");
                    if (customer.CustomerId == 0) await _customers.AddAsync(customer);
                    else await _customers.UpdateAsync(customer);
                }
                else if (_section == "Suppliers" && row is Supplier supplier)
                {
                    if (string.IsNullOrWhiteSpace(supplier.SupplierCode) || string.IsNullOrWhiteSpace(supplier.SupplierName))
                        throw new InvalidOperationException("Each supplier needs a code and a name.");
                    if (supplier.OpeningBalance < 0)
                        throw new InvalidOperationException("Opening balance cannot be negative.");
                    if (supplier.SupplierId == 0) await _suppliers.AddAsync(supplier);
                    else await _suppliers.UpdateAsync(supplier);
                }
            }
            await LoadAsync();
            Announce($"Changes saved. {_section} are up to date.");
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }
}
