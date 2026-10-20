using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Cryptography;
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
    private readonly IAuditLogRepository? _audit;
    private readonly IAdminSession? _session;
    private readonly ICustomerLedgerRepository? _customerLedgers;
    private readonly ISupplierLedgerRepository? _supplierLedgers;
    private readonly IStockMovementRepository? _stockMovements;
    private readonly Dictionary<string, string> _originalSnapshots = new(StringComparer.Ordinal);
    private readonly object _recordsLock = new();
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
                CopySelectedDetailsCommand.RaiseCanExecuteChanged();
                DeleteSelectedCommand.RaiseCanExecuteChanged();
                InitiateDeleteCommand.RaiseCanExecuteChanged();
                EditSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusActionLabel => SelectedRecord?.GetType().GetProperty("Status")?.GetValue(SelectedRecord)?.ToString() == "Inactive"
        ? "Reactivate selected"
        : "Deactivate selected";

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                DeleteSelectedCommand.RaiseCanExecuteChanged();
                InitiateDeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isAddFormOpen;
    private object? _newRecordDraft;
    private string _addFormTitle = string.Empty;

    public bool IsAddFormOpen
    {
        get => _isAddFormOpen;
        set => SetProperty(ref _isAddFormOpen, value);
    }

    public object? NewRecordDraft
    {
        get => _newRecordDraft;
        set => SetProperty(ref _newRecordDraft, value);
    }

    public string AddFormTitle
    {
        get => _addFormTitle;
        set => SetProperty(ref _addFormTitle, value);
    }

    public string SaveNewActionLabel
    {
        get
        {
            bool isEdit = (NewRecordDraft is Product p && p.ProductId > 0)
                       || (NewRecordDraft is Customer c && c.CustomerId > 0)
                       || (NewRecordDraft is Supplier s && s.SupplierId > 0);
            return isEdit ? $"Update {_section.TrimEnd('s')}" : $"Save {_section.TrimEnd('s')}";
        }
    }

    // =========================================================================
    // QUICK FILTERS (WORK ORDER 6)
    // =========================================================================
    private string _selectedStatusFilter = "All";
    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsInactiveFilterSelected));
                FilteredRecords.Refresh();
            }
        }
    }

    public bool IsAllFilterSelected => SelectedStatusFilter == "All";
    public bool IsActiveFilterSelected => SelectedStatusFilter == "Active";
    public bool IsInactiveFilterSelected => SelectedStatusFilter == "Inactive";

    public int AllCount => Records.Count;
    public int ActiveCount => Records.Count(r => GetStatus(r) == "Active");
    public int InactiveCount => Records.Count(r => GetStatus(r) == "Inactive");

    public string AllChipLabel => $"All ({AllCount:N0})";
    public string ActiveChipLabel => $"Active ({ActiveCount:N0})";
    public string InactiveChipLabel => $"Inactive ({InactiveCount:N0})";

    private LookupItem? _selectedCategoryFilter;
    public LookupItem? SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set { if (SetProperty(ref _selectedCategoryFilter, value)) FilteredRecords.Refresh(); }
    }

    private LookupItem? _selectedBrandFilter;
    public LookupItem? SelectedBrandFilter
    {
        get => _selectedBrandFilter;
        set { if (SetProperty(ref _selectedBrandFilter, value)) FilteredRecords.Refresh(); }
    }

    private string? _selectedAreaFilter = "All Areas";
    public string? SelectedAreaFilter
    {
        get => _selectedAreaFilter;
        set { if (SetProperty(ref _selectedAreaFilter, value)) FilteredRecords.Refresh(); }
    }

    public ObservableCollection<string> AvailableAreas { get; } = new();

    public RelayCommand SetStatusFilterCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }

    // =========================================================================
    // DESTRUCTIVE ACTION PROTECTION & TWO-STEP SAFETY (WORK ORDER 3)
    // =========================================================================
    private bool _isDeleteConfirmOpen;
    public bool IsDeleteConfirmOpen
    {
        get => _isDeleteConfirmOpen;
        set => SetProperty(ref _isDeleteConfirmOpen, value);
    }

    private string _deleteConfirmTitle = string.Empty;
    public string DeleteConfirmTitle
    {
        get => _deleteConfirmTitle;
        set => SetProperty(ref _deleteConfirmTitle, value);
    }

    private string _deleteConfirmMessage = string.Empty;
    public string DeleteConfirmMessage
    {
        get => _deleteConfirmMessage;
        set => SetProperty(ref _deleteConfirmMessage, value);
    }

    private int _relatedTransactionCount;
    public int RelatedTransactionCount
    {
        get => _relatedTransactionCount;
        set => SetProperty(ref _relatedTransactionCount, value);
    }

    private bool _canPermanentlyDelete;
    public bool CanPermanentlyDelete
    {
        get => _canPermanentlyDelete;
        set => SetProperty(ref _canPermanentlyDelete, value);
    }

    private string _confirmDeleteText = string.Empty;
    public string ConfirmDeleteText
    {
        get => _confirmDeleteText;
        set
        {
            if (SetProperty(ref _confirmDeleteText, value))
            {
                ConfirmPermanentDeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string EntityNameToDelete { get; private set; } = string.Empty;
    public string EntityCodeToDelete { get; private set; } = string.Empty;

    public AsyncRelayCommand InitiateDeleteCommand { get; }
    public AsyncRelayCommand ConfirmDeactivateCommand { get; }
    public AsyncRelayCommand ConfirmPermanentDeleteCommand { get; }
    public RelayCommand CancelDeleteConfirmCommand { get; }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand AddCommand { get; }
    public AsyncRelayCommand SaveNewRecordCommand { get; }
    public RelayCommand CancelAddCommand { get; }
    public RelayCommand ToggleStatusCommand { get; }
    public RelayCommand CopySelectedDetailsCommand { get; }
    public AsyncRelayCommand DeleteSelectedCommand { get; }
    public RelayCommand EditSelectedCommand { get; }
    public RelayCommand RefreshSearchCommand { get; }

    public MasterDataViewModel(
        string section,
        IProductRepository products,
        ICustomerRepository customers,
        ISupplierRepository suppliers,
        IBusinessLookupRepository? lookups = null,
        IAuditLogRepository? audit = null,
        IAdminSession? session = null,
        ICustomerLedgerRepository? customerLedgers = null,
        ISupplierLedgerRepository? supplierLedgers = null,
        IStockMovementRepository? stockMovements = null)
    {
        _section = section is "Products" or "Customers" or "Suppliers"
            ? section
            : throw new ArgumentOutOfRangeException(nameof(section));
        _products = products;
        _customers = customers;
        _suppliers = suppliers;
        _lookups = lookups;
        _audit = audit;
        _session = session;
        _customerLedgers = customerLedgers;
        _supplierLedgers = supplierLedgers;
        _stockMovements = stockMovements;

        FilteredRecords = CollectionViewSource.GetDefaultView(Records);
        FilteredRecords.Filter = MatchesFilters;
        BindingOperations.EnableCollectionSynchronization(Records, _recordsLock);

        SetStatusFilterCommand = new RelayCommand(param => SelectedStatusFilter = param?.ToString() ?? "All");
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        ClearFiltersCommand = new RelayCommand(ClearAllFilters);

        InitiateDeleteCommand = new AsyncRelayCommand(InitiateDeleteAsync, () => SelectedRecord != null && !IsBusy);
        ConfirmDeactivateCommand = new AsyncRelayCommand(ConfirmDeactivateAsync);
        ConfirmPermanentDeleteCommand = new AsyncRelayCommand(ConfirmPermanentDeleteAsync, () => CanPermanentlyDelete && string.Equals(ConfirmDeleteText?.Trim(), "CONFIRM", StringComparison.OrdinalIgnoreCase));
        CancelDeleteConfirmCommand = new RelayCommand(() => IsDeleteConfirmOpen = false);

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        AddCommand = new RelayCommand(OpenAddForm);
        SaveNewRecordCommand = new AsyncRelayCommand(SaveNewRecordAsync, () => !IsBusy && IsAddFormOpen);
        CancelAddCommand = new RelayCommand(CancelAddForm);
        ToggleStatusCommand = new RelayCommand(ToggleStatus, _ => SelectedRecord != null);
        CopySelectedDetailsCommand = new RelayCommand(_ => CopySelectedDetails(), _ => SelectedRecord != null);
        DeleteSelectedCommand = InitiateDeleteCommand;
        EditSelectedCommand = new RelayCommand(_ => OpenEditForm(SelectedRecord), _ => SelectedRecord != null);
        RefreshSearchCommand = new RelayCommand(() =>
        {
            FilteredRecords.Refresh();
            Announce($"Search applied for '{SearchText}'.");
        });
    }

    private void ClearAllFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = "All";
        SelectedCategoryFilter = null;
        SelectedBrandFilter = null;
        SelectedAreaFilter = "All Areas";
        FilteredRecords.Refresh();
    }

    private static string GetStatus(object? item) =>
        item?.GetType().GetProperty("Status")?.GetValue(item)?.ToString() ?? "Active";

    private bool MatchesFilters(object item)
    {
        // 1. Status Filter
        if (SelectedStatusFilter != "All")
        {
            var status = GetStatus(item);
            if (!string.Equals(status, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 2. Category Filter (Products)
        if (_section == "Products" && SelectedCategoryFilter != null && SelectedCategoryFilter.Id > 0)
        {
            if (item is Product p && p.CategoryId != SelectedCategoryFilter.Id)
                return false;
        }

        // 3. Brand Filter (Products)
        if (_section == "Products" && SelectedBrandFilter != null && SelectedBrandFilter.Id > 0)
        {
            if (item is Product p && p.BrandId != SelectedBrandFilter.Id)
                return false;
        }

        // 4. Area / Territory Filter (Customers)
        if (_section == "Customers" && !string.IsNullOrWhiteSpace(SelectedAreaFilter) && SelectedAreaFilter != "All Areas")
        {
            if (item is Customer c && !string.Equals(c.AreaTerritory?.Trim(), SelectedAreaFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 5. Search Text Filter
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
            if (_section == "Products")
            {
                if (_lookups != null)
                {
                    Categories.Clear(); Brands.Clear(); Units.Clear();
                    foreach (var item in await _lookups.GetAllAsync("Categories")) Categories.Add(item);
                    foreach (var item in await _lookups.GetAllAsync("Brands")) Brands.Add(item);
                    foreach (var item in await _lookups.GetAllAsync("Units")) Units.Add(item);
                }
                var rows = await _products.GetAllAsync();
                lock (_recordsLock)
                {
                    Records.Clear();
                    foreach (var row in rows) Records.Add(row);
                }
            }
            else if (_section == "Customers")
            {
                var rows = await _customers.GetAllAsync();
                lock (_recordsLock)
                {
                    Records.Clear();
                    foreach (var row in rows) Records.Add(row);
                }
                AvailableAreas.Clear();
                AvailableAreas.Add("All Areas");
                foreach (var area in Records.OfType<Customer>()
                    .Select(c => c.AreaTerritory?.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a))
                {
                    AvailableAreas.Add(area!);
                }
            }
            else
            {
                var rows = await _suppliers.GetAllAsync();
                lock (_recordsLock)
                {
                    Records.Clear();
                    foreach (var row in rows) Records.Add(row);
                }
            }
            CaptureOriginalSnapshots();
            RefreshFilterCounts();
            Announce($"{_section} loaded.");
        }
        catch (Exception ex) { SetError($"Could not load {_section.ToLowerInvariant()}. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    public void OpenAddForm()
    {
        ClearErrors();
        AddFormTitle = $"Add New {_section.TrimEnd('s')}";
        NewRecordDraft = _section switch
        {
            "Products" => new Product
            {
                ProductCode = CreateUniqueProductCode(),
                ProductName = string.Empty,
                Status = "Active",
                BasePurchasePrice = 0,
                DefaultSalePrice = 0,
                MinimumStock = 5,
                PurchasePercentage = 0,
                SellingPercentage = 0
            },
            "Customers" => new Customer
            {
                CustomerCode = CreateUniqueCustomerCode(),
                CustomerName = string.Empty,
                Status = "Active",
                OpeningBalance = 0,
                CreditLimit = 0
            },
            _ => new Supplier
            {
                SupplierCode = CreateUniqueSupplierCode(),
                SupplierName = string.Empty,
                Status = "Active",
                OpeningBalance = 0
            }
        };
        OnPropertyChanged(nameof(SaveNewActionLabel));
        IsAddFormOpen = true;
        SaveNewRecordCommand.RaiseCanExecuteChanged();
        Announce($"Opening {AddFormTitle} form.");
    }

    public void OpenEditForm(object? item)
    {
        if (item == null) return;
        ClearErrors();
        SelectedRecord = item;
        if (item is Product p)
        {
            AddFormTitle = $"Edit Product: {p.ProductName}";
            NewRecordDraft = new Product
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId,
                UnitId = p.UnitId,
                BasePurchasePrice = p.BasePurchasePrice,
                DefaultSalePrice = p.DefaultSalePrice,
                MinimumStock = p.MinimumStock,
                CurrentStock = p.CurrentStock,
                PurchasePercentage = p.PurchasePercentage,
                SellingPercentage = p.SellingPercentage,
                Status = p.Status
            };
        }
        else if (item is Customer c)
        {
            AddFormTitle = $"Edit Customer: {c.CustomerName}";
            NewRecordDraft = new Customer
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                ContactPerson = c.ContactPerson,
                Phone = c.Phone,
                Address = c.Address,
                AreaTerritory = c.AreaTerritory,
                OpeningBalance = c.OpeningBalance,
                CreditLimit = c.CreditLimit,
                Status = c.Status
            };
        }
        else if (item is Supplier s)
        {
            AddFormTitle = $"Edit Supplier: {s.SupplierName}";
            NewRecordDraft = new Supplier
            {
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Address = s.Address,
                OpeningBalance = s.OpeningBalance,
                Status = s.Status
            };
        }
        OnPropertyChanged(nameof(SaveNewActionLabel));
        IsAddFormOpen = true;
        SaveNewRecordCommand.RaiseCanExecuteChanged();
        Announce($"Opening {AddFormTitle} form.");
    }

    private void CancelAddForm()
    {
        IsAddFormOpen = false;
        NewRecordDraft = null;
        ClearErrors();
        OnPropertyChanged(nameof(SaveNewActionLabel));
        SaveNewRecordCommand.RaiseCanExecuteChanged();
        Announce("Operation cancelled.");
    }

    private async Task SaveNewRecordAsync()
    {
        if (NewRecordDraft == null) return;
        var draftCopy = NewRecordDraft;
        ClearErrors();
        try
        {
            IsBusy = true;
            SaveNewRecordCommand.RaiseCanExecuteChanged();
            await Task.Yield();
            var auditWarnings = new List<string>();

            bool isNewRecord = false;
            string recordName = string.Empty;

            if (_section == "Products" && NewRecordDraft is Product product)
            {
                if (string.IsNullOrWhiteSpace(product.ProductName))
                    throw new InvalidOperationException("Product Name is required.");
                if (string.IsNullOrWhiteSpace(product.ProductCode))
                    product.ProductCode = CreateUniqueProductCode();
                if (product.BasePurchasePrice < 0 || product.DefaultSalePrice < 0 || product.MinimumStock < 0 || product.PurchasePercentage < 0)
                    throw new InvalidOperationException("Prices, percentages, and minimum stock cannot be negative.");

                isNewRecord = product.ProductId == 0;
                recordName = product.ProductName;
                if (isNewRecord)
                {
                    await _products.AddAsync(product);
                    await RecordMasterDataActionAsync(true, product.ProductCode, null, Snapshot(product), auditWarnings);
                }
                else
                {
                    var before = GetOriginalSnapshot(product, product.ProductId);
                    await _products.UpdateAsync(product);
                    await RecordMasterDataActionAsync(false, product.ProductCode, before, Snapshot(product), auditWarnings);
                }
            }
            else if (_section == "Customers" && NewRecordDraft is Customer customer)
            {
                if (string.IsNullOrWhiteSpace(customer.CustomerName))
                    throw new InvalidOperationException("Customer Name is required.");
                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                    customer.CustomerCode = CreateUniqueCustomerCode();
                if (customer.OpeningBalance < 0 || customer.CreditLimit < 0)
                    throw new InvalidOperationException("Opening balance and credit limit cannot be negative.");

                isNewRecord = customer.CustomerId == 0;
                recordName = customer.CustomerName;
                if (isNewRecord)
                {
                    await _customers.AddAsync(customer);
                    await RecordMasterDataActionAsync(true, customer.CustomerCode, null, Snapshot(customer), auditWarnings);
                }
                else
                {
                    var before = GetOriginalSnapshot(customer, customer.CustomerId);
                    await _customers.UpdateAsync(customer);
                    await RecordMasterDataActionAsync(false, customer.CustomerCode, before, Snapshot(customer), auditWarnings);
                }
            }
            else if (_section == "Suppliers" && NewRecordDraft is Supplier supplier)
            {
                if (string.IsNullOrWhiteSpace(supplier.SupplierName))
                    throw new InvalidOperationException("Supplier Name is required.");
                if (string.IsNullOrWhiteSpace(supplier.SupplierCode))
                    supplier.SupplierCode = CreateUniqueSupplierCode();
                if (supplier.OpeningBalance < 0)
                    throw new InvalidOperationException("Opening balance cannot be negative.");

                isNewRecord = supplier.SupplierId == 0;
                recordName = supplier.SupplierName;
                if (isNewRecord)
                {
                    await _suppliers.AddAsync(supplier);
                    await RecordMasterDataActionAsync(true, supplier.SupplierCode, null, Snapshot(supplier), auditWarnings);
                }
                else
                {
                    var before = GetOriginalSnapshot(supplier, supplier.SupplierId);
                    await _suppliers.UpdateAsync(supplier);
                    await RecordMasterDataActionAsync(false, supplier.SupplierCode, before, Snapshot(supplier), auditWarnings);
                }
            }

            IsAddFormOpen = false;
            NewRecordDraft = null;
            OnPropertyChanged(nameof(SaveNewActionLabel));
            var actionText = isNewRecord ? "added" : "updated";
            var successMessage = $"{_section.TrimEnd('s')} '{recordName}' {actionText} successfully.";
            await LoadAsync();
            Announce(successMessage);
            if (auditWarnings.Count > 0)
                SetError($"Record saved, but activity logging reported: {string.Join(" ", auditWarnings.Distinct())}");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            SaveNewRecordCommand.RaiseCanExecuteChanged();
        }
    }

    private string CreateUniqueProductCode()
    {
        var existingCodes = Records.OfType<Product>()
            .Select(product => product.ProductCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CreateUniqueCode("P", existingCodes);
    }

    private string CreateUniqueCustomerCode() => CreateUniqueCode("C", Records.OfType<Customer>().Select(customer => customer.CustomerCode).ToHashSet(StringComparer.OrdinalIgnoreCase));
    private string CreateUniqueSupplierCode() => CreateUniqueCode("S", Records.OfType<Supplier>().Select(supplier => supplier.SupplierCode).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static string CreateUniqueCode(string prefix, HashSet<string> existingCodes)
    {
        string code;
        do
        {
            code = $"{prefix}-{DateTime.Now:yyMMddHHmmss}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
        } while (!existingCodes.Add(code));
        return code;
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

    private void CopySelectedDetails()
    {
        if (SelectedRecord == null) return;
        try
        {
            var props = SelectedRecord.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.Name is not "CreatedAt" and not "UpdatedAt")
                .Select(p => $"{p.Name}: {p.GetValue(SelectedRecord)}");
            var summary = string.Join(Environment.NewLine, props);
            System.Windows.Clipboard.SetText(summary);
            Announce($"Copied {_section.TrimEnd('s').ToLowerInvariant()} details to clipboard.");
        }
        catch (Exception ex)
        {
            SetError($"Could not copy details: {ex.Message}");
        }
    }

    public async Task InitiateDeleteForRecordAsync(object? item)
    {
        if (item == null) return;
        SelectedRecord = item;
        await InitiateDeleteAsync();
    }

    public async Task InitiateDeleteAsync()
    {
        if (SelectedRecord == null) return;
        ClearErrors();

        var id = GetRecordId(SelectedRecord);
        if (id == 0)
        {
            // Unsaved row draft; remove immediately
            Records.Remove(SelectedRecord);
            SelectedRecord = null;
            Announce($"Removed unsaved {_section.TrimEnd('s').ToLowerInvariant()} draft.");
            RefreshFilterCounts();
            return;
        }

        try
        {
            IsBusy = true;
            int count = 0;
            if (_section == "Customers" && SelectedRecord is Customer customer)
            {
                EntityCodeToDelete = customer.CustomerCode;
                EntityNameToDelete = customer.CustomerName;
                if (_customerLedgers != null)
                {
                    var ledgerEntries = await _customerLedgers.GetByCustomerIdAsync(customer.CustomerId);
                    count = ledgerEntries.Count;
                }
            }
            else if (_section == "Suppliers" && SelectedRecord is Supplier supplier)
            {
                EntityCodeToDelete = supplier.SupplierCode;
                EntityNameToDelete = supplier.SupplierName;
                if (_supplierLedgers != null)
                {
                    var ledgerEntries = await _supplierLedgers.GetBySupplierIdAsync(supplier.SupplierId);
                    count = ledgerEntries.Count;
                }
            }
            else if (_section == "Products" && SelectedRecord is Product product)
            {
                EntityCodeToDelete = product.ProductCode;
                EntityNameToDelete = product.ProductName;
                if (_stockMovements != null)
                {
                    var movements = await _stockMovements.GetByProductIdAsync(product.ProductId);
                    count = movements.Count;
                }
            }

            RelatedTransactionCount = count;
            CanPermanentlyDelete = count == 0;
            ConfirmDeleteText = string.Empty;

            if (count > 0)
            {
                DeleteConfirmTitle = $"Cannot Delete {_section.TrimEnd('s')} (Has Transactions)";
                DeleteConfirmMessage = $"Are you sure you want to delete {_section.TrimEnd('s')} '{EntityNameToDelete}' (Code: {EntityCodeToDelete})? This {_section.TrimEnd('s').ToLowerInvariant()} has {count} past transaction(s). To prevent ledger discrepancies, deletion is blocked. Please change status to Inactive instead.";
            }
            else
            {
                DeleteConfirmTitle = $"Confirm Deletion of {_section.TrimEnd('s')}";
                DeleteConfirmMessage = $"Are you sure you want to permanently delete {_section.TrimEnd('s')} '{EntityNameToDelete}' (Code: {EntityCodeToDelete})? This {_section.TrimEnd('s').ToLowerInvariant()} has 0 past transactions. Type CONFIRM below to proceed.";
            }

            IsDeleteConfirmOpen = true;
        }
        catch (Exception ex)
        {
            SetError($"Could not verify transaction count: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ConfirmDeactivateAsync()
    {
        if (SelectedRecord == null) return;
        IsDeleteConfirmOpen = false;
        ClearErrors();
        try
        {
            IsBusy = true;
            var statusProp = SelectedRecord.GetType().GetProperty("Status");
            if (statusProp != null)
            {
                statusProp.SetValue(SelectedRecord, "Inactive");
                FilteredRecords.Refresh();
                OnPropertyChanged(nameof(StatusActionLabel));
            }

            if (_section == "Products" && SelectedRecord is Product product)
            {
                await _products.UpdateAsync(product);
                Announce($"Product '{product.ProductName}' has been deactivated safely.");
            }
            else if (_section == "Customers" && SelectedRecord is Customer customer)
            {
                await _customers.UpdateAsync(customer);
                Announce($"Customer '{customer.CustomerName}' has been deactivated safely.");
            }
            else if (_section == "Suppliers" && SelectedRecord is Supplier supplier)
            {
                await _suppliers.UpdateAsync(supplier);
                Announce($"Supplier '{supplier.SupplierName}' has been deactivated safely.");
            }

            RefreshFilterCounts();
        }
        catch (Exception ex)
        {
            SetError($"Could not deactivate {_section.TrimEnd('s').ToLowerInvariant()}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ConfirmPermanentDeleteAsync()
    {
        if (SelectedRecord == null || !CanPermanentlyDelete || !string.Equals(ConfirmDeleteText?.Trim(), "CONFIRM", StringComparison.OrdinalIgnoreCase))
            return;

        IsDeleteConfirmOpen = false;
        ClearErrors();
        try
        {
            IsBusy = true;
            await Task.Yield();
            var recordToDelete = SelectedRecord;
            Records.Remove(recordToDelete);
            SelectedRecord = null;
            RefreshFilterCounts();
            Announce($"{_section.TrimEnd('s')} '{EntityNameToDelete}' has been permanently deleted.");
        }
        catch (Exception ex)
        {
            SetError($"Could not delete {_section.TrimEnd('s').ToLowerInvariant()}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshFilterCounts()
    {
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(InactiveCount));
        OnPropertyChanged(nameof(AllChipLabel));
        OnPropertyChanged(nameof(ActiveChipLabel));
        OnPropertyChanged(nameof(InactiveChipLabel));
    }

    private async Task SaveAsync()
    {
        var auditWarnings = new List<string>();
        try
        {
            IsBusy = true;
            ClearErrors();
            // Older builds generated customer/supplier codes only to the second, so
            // multiple unsaved rows could share one. Repair duplicate staged codes
            // before writing any rows; randomized codes also avoid cross-session races.
            var pendingProductCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingCustomerCodes = Records.OfType<Customer>().Where(customer => customer.CustomerId > 0)
                .Select(customer => customer.CustomerCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pendingSupplierCodes = Records.OfType<Supplier>().Where(supplier => supplier.SupplierId > 0)
                .Select(supplier => supplier.SupplierCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var customer in Records.OfType<Customer>().Where(customer => customer.CustomerId == 0))
            {
                if (!pendingCustomerCodes.Add(customer.CustomerCode))
                    customer.CustomerCode = CreateUniqueCode("C", pendingCustomerCodes);
                pendingCustomerCodes.Add(customer.CustomerCode);
            }
            foreach (var supplier in Records.OfType<Supplier>().Where(supplier => supplier.SupplierId == 0))
            {
                if (!pendingSupplierCodes.Add(supplier.SupplierCode))
                    supplier.SupplierCode = CreateUniqueCode("S", pendingSupplierCodes);
                pendingSupplierCodes.Add(supplier.SupplierCode);
            }
            foreach (var row in Records.ToArray())
            {
                if (_section == "Products" && row is Product product)
                {
                    if (product.ProductId == 0 && !pendingProductCodes.Add(product.ProductCode))
                    {
                        product.ProductCode = CreateUniqueProductCode();
                        pendingProductCodes.Add(product.ProductCode);
                    }
                    if (string.IsNullOrWhiteSpace(product.ProductCode) || string.IsNullOrWhiteSpace(product.ProductName))
                        throw new InvalidOperationException("Each product needs a code and a name.");
                    if (product.BasePurchasePrice < 0 || product.DefaultSalePrice < 0 || product.MinimumStock < 0 || product.PurchasePercentage < 0)
                        throw new InvalidOperationException("Prices, purchase reference percentage, and minimum stock cannot be negative.");
                    var isNew = product.ProductId == 0;
                    var before = isNew ? null : GetOriginalSnapshot(product, product.ProductId);
                    if (!isNew && before == Snapshot(product)) continue;
                    if (isNew) await _products.AddAsync(product);
                    else await _products.UpdateAsync(product);
                    await RecordMasterDataActionAsync(isNew, product.ProductCode, before, Snapshot(product), auditWarnings);
                }
                else if (_section == "Customers" && row is Customer customer)
                {
                    if (string.IsNullOrWhiteSpace(customer.CustomerCode) || string.IsNullOrWhiteSpace(customer.CustomerName))
                        throw new InvalidOperationException("Each customer needs a code and a name.");
                    if (customer.OpeningBalance < 0 || customer.CreditLimit < 0)
                        throw new InvalidOperationException("Opening balance and credit limit cannot be negative.");
                    var isNew = customer.CustomerId == 0;
                    var before = isNew ? null : GetOriginalSnapshot(customer, customer.CustomerId);
                    if (!isNew && before == Snapshot(customer)) continue;
                    if (isNew) await _customers.AddAsync(customer);
                    else await _customers.UpdateAsync(customer);
                    await RecordMasterDataActionAsync(isNew, customer.CustomerCode, before, Snapshot(customer), auditWarnings);
                }
                else if (_section == "Suppliers" && row is Supplier supplier)
                {
                    if (string.IsNullOrWhiteSpace(supplier.SupplierCode) || string.IsNullOrWhiteSpace(supplier.SupplierName))
                        throw new InvalidOperationException("Each supplier needs a code and a name.");
                    if (supplier.OpeningBalance < 0)
                        throw new InvalidOperationException("Opening balance cannot be negative.");
                    var isNew = supplier.SupplierId == 0;
                    var before = isNew ? null : GetOriginalSnapshot(supplier, supplier.SupplierId);
                    if (!isNew && before == Snapshot(supplier)) continue;
                    if (isNew) await _suppliers.AddAsync(supplier);
                    else await _suppliers.UpdateAsync(supplier);
                    await RecordMasterDataActionAsync(isNew, supplier.SupplierCode, before, Snapshot(supplier), auditWarnings);
                }
            }
            await LoadAsync();
            if (HasErrors) return;
            if (auditWarnings.Count > 0)
                SetError($"Changes were saved, but activity logging needs attention: {string.Join(" ", auditWarnings.Distinct())}");
            else Announce($"Changes saved. {_section} are up to date.");
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    private void CaptureOriginalSnapshots()
    {
        _originalSnapshots.Clear();
        foreach (var record in Records)
        {
            var id = GetRecordId(record);
            if (id > 0) _originalSnapshots[GetSnapshotKey(record, id)] = Snapshot(record);
        }
    }

    private string? GetOriginalSnapshot(object record, int id) => _originalSnapshots.TryGetValue(GetSnapshotKey(record, id), out var value) ? value : null;
    private static string GetSnapshotKey(object record, int id) => $"{record.GetType().Name}:{id}";
    private static int GetRecordId(object record) => record switch
    {
        Product product => product.ProductId,
        Customer customer => customer.CustomerId,
        Supplier supplier => supplier.SupplierId,
        _ => 0
    };
    private static string Snapshot(object record) => JsonSerializer.Serialize(record, record.GetType());

    private async Task RecordMasterDataActionAsync(bool isNew, string reference, string? previous, string current, List<string> warnings)
    {
        if (_audit == null || _session == null || _session.AdminId <= 0) return;
        try
        {
            await _audit.LogActionAsync(_session.AdminId, isNew ? "CREATE" : "UPDATE", _section, reference,
                isNew ? null : previous, current);
        }
        catch (Exception ex) { warnings.Add(ex.Message); }
    }
}
