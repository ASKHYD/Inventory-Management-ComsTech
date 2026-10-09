using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class CashBankViewModel : ViewModelBase
{
    private readonly ICashTransactionRepository _cash;
    private readonly IBankTransactionRepository _bank;
    private decimal _cashBalance;
    private decimal _bankBalance;
    private bool _isBusy;

    public ObservableCollection<CashTransaction> CashTransactions { get; } = new();
    public ObservableCollection<BankTransaction> BankTransactions { get; } = new();
    public decimal CashBalance { get => _cashBalance; private set => SetProperty(ref _cashBalance, value); }
    public decimal BankBalance { get => _bankBalance; private set => SetProperty(ref _bankBalance, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public AsyncRelayCommand LoadCommand { get; }

    public CashBankViewModel(ICashTransactionRepository cash, IBankTransactionRepository bank)
    {
        _cash = cash ?? throw new ArgumentNullException(nameof(cash));
        _bank = bank ?? throw new ArgumentNullException(nameof(bank));
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            CashTransactions.Clear();
            BankTransactions.Clear();
            var cashRows = await _cash.GetAllAsync();
            foreach (var row in cashRows.OrderByDescending(row => row.TransactionDate)) CashTransactions.Add(row);
            var bankRows = await _bank.GetAllAsync();
            foreach (var row in bankRows.OrderByDescending(row => row.TransactionDate)) BankTransactions.Add(row);
            CashBalance = await _cash.GetCurrentBalanceAsync();
            BankBalance = await _bank.GetCurrentBalanceAsync();
            Announce("Cash and bank balances refreshed.");
        }
        catch (Exception ex) { SetError($"Could not load account activity. {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
