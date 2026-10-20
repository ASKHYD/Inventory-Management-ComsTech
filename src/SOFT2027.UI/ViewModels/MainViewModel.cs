using System;
using System.Linq;
using System.Windows.Input;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Func<SalesEntryViewModel> _salesViewModelFactory;
        private readonly Func<ReportsDashboardViewModel> _reportsViewModelFactory;
        private readonly Func<string, MasterDataViewModel>? _masterDataViewModelFactory;
        private readonly Func<PurchaseEntryViewModel>? _purchaseViewModelFactory;
        private readonly IAdminUserRepository? _adminUsers;
        private readonly IAdminSession? _adminSession;
        private readonly Func<string, FinancialEntryViewModel>? _financialEntryFactory;
        private readonly Func<InventoryViewModel>? _inventoryViewModelFactory;
        private readonly Func<CashBankViewModel>? _cashBankViewModelFactory;
        private readonly Func<BackupViewModel>? _backupViewModelFactory;
        private readonly Func<bool, ReturnEntryViewModel>? _returnViewModelFactory;
        private readonly Func<bool, LedgerViewModel>? _ledgerViewModelFactory;
        private readonly Func<AdministrationViewModel>? _administrationViewModelFactory;
        private readonly Func<LookupManagementViewModel>? _lookupManagementViewModelFactory;
        private ViewModelBase? _currentViewModel;
        private bool _isAuthenticated;
        private string _loginUsername = string.Empty;
        private string _loginPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _fullName = string.Empty;
        private bool _isFirstRunSetup;
        private string _userDisplayName = "Administrator";
        private bool _isBusy;
        private string _busyMessage = string.Empty;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set => SetProperty(ref _isAuthenticated, value);
        }

        public bool IsAdministrator => _adminSession?.IsAdministrator ?? true;

        private bool _isSidebarVisible = true;

        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set => SetProperty(ref _isSidebarVisible, value);
        }

        public RelayCommand ToggleSidebarCommand { get; }

        public string LoginUsername
        {
            get => _loginUsername;
            set => SetProperty(ref _loginUsername, value);
        }

        public string LoginPassword
        {
            get => _loginPassword;
            set => SetProperty(ref _loginPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public bool IsFirstRunSetup
        {
            get => _isFirstRunSetup;
            private set
            {
                if (SetProperty(ref _isFirstRunSetup, value))
                {
                    OnPropertyChanged(nameof(LoginHeading));
                    OnPropertyChanged(nameof(LoginButtonText));
                    OnPropertyChanged(nameof(LoginHint));
                }
            }
        }

        public string LoginHeading => IsFirstRunSetup ? "Set up your administrator account" : "Sign In";
        public string LoginButtonText => IsFirstRunSetup ? "Create Administrator" : "Sign In";
        public string LoginHint => IsFirstRunSetup
            ? "This one-time setup protects your local business records. Use at least 10 characters for the password."
            : "Sign in with your username and password.";
        public string UserDisplayName
        {
            get => _userDisplayName;
            private set
            {
                if (SetProperty(ref _userDisplayName, value)) OnPropertyChanged(nameof(UserInitials));
            }
        }
        public string UserRole => _adminSession?.Role ?? "Administrator";
        public string UserInitials => string.Join(string.Empty, UserDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0])).ToUpperInvariant();

        public ViewModelBase? CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }

        public ICommand NavigateToSalesCommand { get; }
        public ICommand NavigateToPurchasesCommand { get; }
        public ICommand NavigateToReportsCommand { get; }
        public ICommand NavigateToProductsCommand { get; }
        public ICommand NavigateToCustomersCommand { get; }
        public ICommand NavigateToSuppliersCommand { get; }
        public ICommand NavigateToReceiptsCommand { get; }
        public ICommand NavigateToPaymentsCommand { get; }
        public ICommand NavigateToExpensesCommand { get; }
        public ICommand NavigateToInventoryCommand { get; }
        public ICommand NavigateToCashBankCommand { get; }
        public ICommand NavigateToBackupCommand { get; }
        public ICommand NavigateToSalesReturnsCommand { get; }
        public ICommand NavigateToPurchaseReturnsCommand { get; }
        public ICommand NavigateToCustomerLedgerCommand { get; }
        public ICommand NavigateToSupplierLedgerCommand { get; }
        public ICommand NavigateToAdministrationCommand { get; }
        public ICommand NavigateToLookupsCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, null, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, purchaseViewModelFactory, null, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory,
            IAdminUserRepository? adminUsers,
            IAdminSession? adminSession)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, purchaseViewModelFactory, adminUsers, adminSession, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory,
            IAdminUserRepository? adminUsers,
            IAdminSession? adminSession,
            Func<string, FinancialEntryViewModel>? financialEntryFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, purchaseViewModelFactory, adminUsers, adminSession, financialEntryFactory, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory,
            IAdminUserRepository? adminUsers,
            IAdminSession? adminSession,
            Func<string, FinancialEntryViewModel>? financialEntryFactory,
            Func<InventoryViewModel>? inventoryViewModelFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, purchaseViewModelFactory, adminUsers, adminSession, financialEntryFactory, inventoryViewModelFactory, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory,
            IAdminUserRepository? adminUsers,
            IAdminSession? adminSession,
            Func<string, FinancialEntryViewModel>? financialEntryFactory,
            Func<InventoryViewModel>? inventoryViewModelFactory,
            Func<CashBankViewModel>? cashBankViewModelFactory)
            : this(salesViewModelFactory, reportsViewModelFactory, masterDataViewModelFactory, purchaseViewModelFactory, adminUsers, adminSession, financialEntryFactory, inventoryViewModelFactory, cashBankViewModelFactory, null)
        {
        }

        public MainViewModel(
            Func<SalesEntryViewModel> salesViewModelFactory,
            Func<ReportsDashboardViewModel> reportsViewModelFactory,
            Func<string, MasterDataViewModel>? masterDataViewModelFactory,
            Func<PurchaseEntryViewModel>? purchaseViewModelFactory,
            IAdminUserRepository? adminUsers,
            IAdminSession? adminSession,
            Func<string, FinancialEntryViewModel>? financialEntryFactory,
            Func<InventoryViewModel>? inventoryViewModelFactory,
            Func<CashBankViewModel>? cashBankViewModelFactory,
            Func<BackupViewModel>? backupViewModelFactory,
            Func<bool, ReturnEntryViewModel>? returnViewModelFactory = null,
            Func<bool, LedgerViewModel>? ledgerViewModelFactory = null,
            Func<AdministrationViewModel>? administrationViewModelFactory = null,
            Func<LookupManagementViewModel>? lookupManagementViewModelFactory = null)
        {
            _salesViewModelFactory = salesViewModelFactory ?? throw new ArgumentNullException(nameof(salesViewModelFactory));
            _reportsViewModelFactory = reportsViewModelFactory ?? throw new ArgumentNullException(nameof(reportsViewModelFactory));
            _masterDataViewModelFactory = masterDataViewModelFactory;
            _purchaseViewModelFactory = purchaseViewModelFactory;
            _adminUsers = adminUsers;
            _adminSession = adminSession;
            _financialEntryFactory = financialEntryFactory;
            _inventoryViewModelFactory = inventoryViewModelFactory;
            _cashBankViewModelFactory = cashBankViewModelFactory;
            _backupViewModelFactory = backupViewModelFactory;
            _returnViewModelFactory = returnViewModelFactory;
            _ledgerViewModelFactory = ledgerViewModelFactory;
            _administrationViewModelFactory = administrationViewModelFactory;
            _lookupManagementViewModelFactory = lookupManagementViewModelFactory;

            NavigateToSalesCommand = new RelayCommand(NavigateToSales);
            NavigateToPurchasesCommand = new RelayCommand(NavigateToPurchases);
            NavigateToReportsCommand = new RelayCommand(NavigateToReports);
            NavigateToProductsCommand = new RelayCommand(() => NavigateToMasterData("Products"));
            NavigateToCustomersCommand = new RelayCommand(() => NavigateToMasterData("Customers"));
            NavigateToSuppliersCommand = new RelayCommand(() => NavigateToMasterData("Suppliers"));
            NavigateToReceiptsCommand = new RelayCommand(() => NavigateToFinancialEntry("Receipt"));
            NavigateToPaymentsCommand = new RelayCommand(() => NavigateToFinancialEntry("Payment"));
            NavigateToExpensesCommand = new RelayCommand(() => NavigateToFinancialEntry("Expense"));
            NavigateToInventoryCommand = new RelayCommand(NavigateToInventory);
            NavigateToCashBankCommand = new RelayCommand(NavigateToCashBank);
            NavigateToBackupCommand = new RelayCommand(NavigateToBackup);
            NavigateToSalesReturnsCommand = new RelayCommand(() => NavigateToReturns(true));
            NavigateToPurchaseReturnsCommand = new RelayCommand(() => NavigateToReturns(false));
            NavigateToCustomerLedgerCommand = new RelayCommand(() => NavigateToLedger(true));
            NavigateToSupplierLedgerCommand = new RelayCommand(() => NavigateToLedger(false));
            NavigateToAdministrationCommand = new RelayCommand(NavigateToAdministration);
            NavigateToLookupsCommand = new RelayCommand(NavigateToLookups);
            ToggleSidebarCommand = new RelayCommand(() => IsSidebarVisible = !IsSidebarVisible);
            LoginCommand = new AsyncRelayCommand(LoginOrSetupAsync);
            LogoutCommand = new RelayCommand(ExecuteLogout);
        }

        public void ExecuteLogin()
        {
            LoginOrSetupAsync().GetAwaiter().GetResult();
        }

        public async Task InitializeSecurityAsync()
        {
            if (_adminUsers == null)
            {
                SetError("Administrator sign-in is unavailable. Contact your system administrator.");
                return;
            }
            try
            {
                IsFirstRunSetup = await _adminUsers.GetUserCountAsync() == 0;
                ClearErrors();
            }
            catch (Exception ex)
            {
                IsFirstRunSetup = false;
                SetError($"Could not check administrator setup. {ex.Message}");
            }
        }

        private async Task LoginOrSetupAsync()
        {
            if (_adminUsers == null || _adminSession == null)
            {
                SetError("Administrator sign-in is not configured.");
                return;
            }
            if (string.IsNullOrWhiteSpace(LoginUsername)) { SetError("Enter your username."); return; }
            if (string.IsNullOrWhiteSpace(LoginPassword)) { SetError("Enter your password."); return; }

            try
            {
                IsBusy = true;
                BusyMessage = IsFirstRunSetup ? "Creating administrator account…" : "Signing in…";
                ClearErrors();
                await Task.Yield();
                AdminUser? user;
                if (IsFirstRunSetup)
                {
                    if (string.IsNullOrWhiteSpace(FullName)) { SetError("Enter your name for the administrator account."); return; }
                    if (LoginUsername.Trim().Length < 3) { SetError("Username must contain at least 3 characters."); return; }
                    if (LoginPassword.Length < 10) { SetError("Choose a password with at least 10 characters."); return; }
                    if (!string.Equals(LoginPassword, ConfirmPassword, StringComparison.Ordinal)) { SetError("The passwords do not match. Enter them again."); return; }
                    user = await _adminUsers.CreateFirstAdminAsync(LoginUsername, FullName, LoginPassword);
                    IsFirstRunSetup = false;
                }
                else
                {
                    user = await _adminUsers.AuthenticateAsync(LoginUsername, LoginPassword);
                    if (user == null) { SetError("Username or password was not recognized. Check your details and try again."); return; }
                }

                _adminSession.SignIn(user);
                OnPropertyChanged(nameof(IsAdministrator));
                OnPropertyChanged(nameof(UserRole));
                UserDisplayName = user.FullName;
                LoginPassword = string.Empty;
                ConfirmPassword = string.Empty;
                IsAuthenticated = true;
                NavigateToSales();
                Announce($"Welcome, {user.FullName}.");
            }
            catch (Exception ex) { SetError($"Sign-in could not be completed. {ex.Message}"); }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        public void ExecuteLogout()
        {
            _adminSession?.SignOut();
            OnPropertyChanged(nameof(IsAdministrator));
            OnPropertyChanged(nameof(UserRole));
            IsAuthenticated = false;
            UserDisplayName = "Administrator";
            LoginPassword = string.Empty;
            ConfirmPassword = string.Empty;
            ClearErrors();
            Announce("Signed out");
        }

        public void NavigateToSales()
        {
            CurrentViewModel = _salesViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Navigated to Sales Entry");
        }

        public void NavigateToReports()
        {
            CurrentViewModel = _reportsViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Navigated to Reports Dashboard");
        }

        public void NavigateToPurchases()
        {
            if (_purchaseViewModelFactory == null) return;
            CurrentViewModel = _purchaseViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Purchase Entry");
        }

        public void NavigateToMasterData(string section)
        {
            if (_masterDataViewModelFactory == null) return;
            CurrentViewModel = _masterDataViewModelFactory(section);
            IsAuthenticated = true;
            ClearErrors();
            Announce($"Opened {section}");
        }

        public void NavigateToFinancialEntry(string kind)
        {
            if (_financialEntryFactory == null) return;
            CurrentViewModel = _financialEntryFactory(kind);
            IsAuthenticated = true;
            ClearErrors();
            Announce($"Opened {kind}");
        }

        public void NavigateToInventory()
        {
            if (_inventoryViewModelFactory == null) return;
            CurrentViewModel = _inventoryViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Inventory");
        }

        public void NavigateToCashBank()
        {
            if (_cashBankViewModelFactory == null) return;
            CurrentViewModel = _cashBankViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Cash and Bank");
        }

        public void NavigateToBackup()
        {
            if (!EnsureAdministrator("Backup and restore")) return;
            if (_backupViewModelFactory == null) return;
            CurrentViewModel = _backupViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Backup and Restore");
        }

        public void NavigateToReturns(bool saleReturn)
        {
            if (_returnViewModelFactory == null) return;
            CurrentViewModel = _returnViewModelFactory(saleReturn);
            IsAuthenticated = true;
            ClearErrors();
            Announce(saleReturn ? "Opened Sales Returns" : "Opened Purchase Returns");
        }

        public void NavigateToLedger(bool customerLedger)
        {
            if (_ledgerViewModelFactory == null) return;
            CurrentViewModel = _ledgerViewModelFactory(customerLedger);
            IsAuthenticated = true;
            ClearErrors();
            Announce(customerLedger ? "Opened Customer Ledger" : "Opened Supplier Ledger");
        }

        public void NavigateToAdministration()
        {
            if (!EnsureAdministrator("Administration")) return;
            if (_administrationViewModelFactory == null) return;
            CurrentViewModel = _administrationViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Administration");
        }

        public void NavigateToLookups()
        {
            if (!EnsureAdministrator("Category, brand and unit management")) return;
            if (_lookupManagementViewModelFactory == null) return;
            CurrentViewModel = _lookupManagementViewModelFactory();
            IsAuthenticated = true;
            ClearErrors();
            Announce("Opened Categories, Brands and Units");
        }

        private bool EnsureAdministrator(string action)
        {
            if (_adminSession?.IsAdministrator == true) return true;
            SetError($"Administrator access is required for {action.ToLowerInvariant()}.");
            return false;
        }
    }
}
