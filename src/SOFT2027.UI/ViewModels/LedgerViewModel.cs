using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

    public LedgerViewModel(bool customerLedger, ICustomerRepository customers, ISupplierRepository suppliers, ICustomerLedgerRepository customerEntries, ISupplierLedgerRepository supplierEntries)
    {
        _customerLedger = customerLedger; _customers = customers; _suppliers = suppliers; _customerEntries = customerEntries; _supplierEntries = supplierEntries;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LoadLedgerCommand = new AsyncRelayCommand(LoadLedgerAsync);
    }
    public string Title => _customerLedger ? "Customer Ledger" : "Supplier Ledger";
    public ObservableCollection<LedgerPartyOption> Parties { get; } = new();
    public ObservableCollection<object> Entries { get; } = new();
    public LedgerPartyOption? SelectedParty { get => _selectedParty; set => SetProperty(ref _selectedParty, value); }
    public string BalanceText { get => _balanceText; private set => SetProperty(ref _balanceText, value); }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand LoadLedgerCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            Parties.Clear(); Entries.Clear();
            if (_customerLedger) foreach (var item in await _customers.GetAllAsync()) Parties.Add(new(item.CustomerId, $"{item.CustomerCode} · {item.CustomerName}", item.OutstandingBalance));
            else foreach (var item in await _suppliers.GetAllAsync()) Parties.Add(new(item.SupplierId, $"{item.SupplierCode} · {item.SupplierName}", item.OutstandingBalance));
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
                foreach (var row in await _customerEntries.GetByCustomerIdAsync(SelectedParty!.Id)) Entries.Add(row);
                BalanceText = $"Current balance: {SelectedParty!.Balance:N2}";
            }
            else
            {
                foreach (var row in await _supplierEntries.GetBySupplierIdAsync(SelectedParty!.Id)) Entries.Add(row);
                BalanceText = $"Current balance: {SelectedParty!.Balance:N2}";
            }
        }
        catch (Exception ex) { SetError($"Could not load ledger. {ex.Message}"); }
    }
}
