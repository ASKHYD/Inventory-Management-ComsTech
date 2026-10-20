using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Data;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class LedgerViewModel : ViewModelBase
{
    public sealed record LedgerPartyOption(int Id, string Name, decimal Balance);
    private readonly bool _customerLedger;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly ICustomerLedgerRepository _customerEntries;
    private readonly ISupplierLedgerRepository _supplierEntries;
    private LedgerPartyOption? _selectedParty;
    private string _balanceText = "Select a customer or supplier to view its ledger.";
    private string _searchText = string.Empty;
    private string _selectedTypeFilter = "All";

    public LedgerViewModel(bool customerLedger, ICustomerRepository customers, ISupplierRepository suppliers, ICustomerLedgerRepository customerEntries, ISupplierLedgerRepository supplierEntries)
    {
        _customerLedger = customerLedger;
        _customers = customers;
        _suppliers = suppliers;
        _customerEntries = customerEntries;
        _supplierEntries = supplierEntries;

        FilteredEntries = CollectionViewSource.GetDefaultView(Entries);
        FilteredEntries.Filter = MatchesFilter;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LoadLedgerCommand = new AsyncRelayCommand(LoadLedgerAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        SetTypeFilterCommand = new RelayCommand(param => SelectedTypeFilter = param?.ToString() ?? "All");
    }

    public string Title => _customerLedger ? "Customer Ledger" : "Supplier Ledger";
    public ObservableCollection<LedgerPartyOption> Parties { get; } = new();
    public ObservableCollection<object> Entries { get; } = new();
    public ICollectionView FilteredEntries { get; }

    public LedgerPartyOption? SelectedParty { get => _selectedParty; set => SetProperty(ref _selectedParty, value); }
    public string BalanceText { get => _balanceText; private set => SetProperty(ref _balanceText, value); }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredEntries.Refresh();
            }
        }
    }

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                FilteredEntries.Refresh();
            }
        }
    }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand LoadLedgerCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand SetTypeFilterCommand { get; }

    private bool MatchesFilter(object item)
    {
        // 1. Transaction Type filter
        if (SelectedTypeFilter != "All")
        {
            if (item is CustomerLedgerEntry cle)
            {
                if (SelectedTypeFilter == "Sales/Debit" && cle.Debit <= 0) return false;
                if (SelectedTypeFilter == "Receipts/Credit" && cle.Credit <= 0) return false;
            }
            else if (item is SupplierLedgerEntry sle)
            {
                if (SelectedTypeFilter == "Purchases/Credit" && sle.Credit <= 0) return false;
                if (SelectedTypeFilter == "Payments/Debit" && sle.Debit <= 0) return false;
            }
        }

        // 2. Search Text filter
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var term = SearchText.Trim();
        if (item is CustomerLedgerEntry c)
        {
            return (c.ReferenceNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.TransactionType?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        if (item is SupplierLedgerEntry s)
        {
            return (s.ReferenceNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.TransactionType?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    public async Task LoadAsync()
    {
        try
        {
            Parties.Clear(); Entries.Clear();
            if (_customerLedger)
            {
                foreach (var item in await _customers.GetAllAsync())
                    Parties.Add(new(item.CustomerId, $"{item.CustomerCode} · {item.CustomerName}", item.OutstandingBalance));
            }
            else
            {
                foreach (var item in await _suppliers.GetAllAsync())
                    Parties.Add(new(item.SupplierId, $"{item.SupplierCode} · {item.SupplierName}", item.OutstandingBalance));
            }
        }
        catch (Exception ex) { SetError($"Could not load account list. {ex.Message}"); }
    }

    private async Task LoadLedgerAsync()
    {
        try
        {
            if (SelectedParty == null) throw new InvalidOperationException("Choose an account first.");
            Entries.Clear();
            if (_customerLedger)
            {
                foreach (var row in await _customerEntries.GetByCustomerIdAsync(SelectedParty!.Id))
                    Entries.Add(row);
                BalanceText = $"Current balance: PKR {SelectedParty!.Balance:N2}";
            }
            else
            {
                foreach (var row in await _supplierEntries.GetBySupplierIdAsync(SelectedParty!.Id))
                    Entries.Add(row);
                BalanceText = $"Current balance: PKR {SelectedParty!.Balance:N2}";
            }
            FilteredEntries.Refresh();
        }
        catch (Exception ex) { SetError($"Could not load ledger. {ex.Message}"); }
    }
}
