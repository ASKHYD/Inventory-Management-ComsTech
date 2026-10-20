using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class AdministrationViewModel : ViewModelBase
{
    private readonly IAdminUserRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IAdminSession _session;
    private string _username = string.Empty, _fullName = string.Empty, _password = string.Empty;
    private string _newUserRole = "Employee";
    private AdminUserSummary? _selectedUser;

    private bool _isBusy;
    private string _busyMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CreateUserCommand.RaiseCanExecuteChanged();
                ToggleUserCommand.RaiseCanExecuteChanged();
                ResetPasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public AdministrationViewModel(IAdminUserRepository users, IAuditLogRepository audit, IAdminSession session)
    {
        _users = users; _audit = audit; _session = session;
        LoadUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
        LoadAuditCommand = new AsyncRelayCommand(LoadAuditAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync, () => !IsBusy);
        ToggleUserCommand = new AsyncRelayCommand(ToggleUserAsync, () => !IsBusy && SelectedUser != null);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => !IsBusy && SelectedUser != null);
    }
    public ObservableCollection<AdminUserSummary> Users { get; } = new();
    public ObservableCollection<AuditLogEntry> AuditEntries { get; } = new();
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string NewUserRole { get => _newUserRole; set => SetProperty(ref _newUserRole, value); }
    public string[] AvailableRoles { get; } = ["Employee", "Administrator"];
    public AdminUserSummary? SelectedUser { get => _selectedUser; set { if (SetProperty(ref _selectedUser, value)) { ToggleUserCommand.RaiseCanExecuteChanged(); ResetPasswordCommand.RaiseCanExecuteChanged(); } } }
    public AsyncRelayCommand LoadUsersCommand { get; }
    public AsyncRelayCommand LoadAuditCommand { get; }
    public AsyncRelayCommand CreateUserCommand { get; }
    public AsyncRelayCommand ToggleUserCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }

    public async Task LoadUsersAsync()
    {
        try { IsBusy = true; Users.Clear(); foreach (var item in await _users.GetAllAsync()) Users.Add(item); }
        catch (Exception ex) { SetError($"Could not load user accounts. {ex.Message}"); }
        finally { IsBusy = false; }
    }
    public async Task LoadAuditAsync()
    {
        try { IsBusy = true; AuditEntries.Clear(); foreach (var item in await _audit.GetRecentAsync()) AuditEntries.Add(item); }
        catch (Exception ex) { SetError($"Could not load the activity history. {ex.Message}"); }
        finally { IsBusy = false; }
    }
    private async Task CreateUserAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length < 3) throw new InvalidOperationException("Enter a username with at least 3 characters.");
            if (string.IsNullOrWhiteSpace(FullName)) throw new InvalidOperationException("Enter the user's full name.");
            if (Password.Length < 10) throw new InvalidOperationException("Choose a password with at least 10 characters.");
            IsBusy = true;
            BusyMessage = "Saving user…";
            var createdRole = NewUserRole;
            await _users.CreateAdminAsync(Username, FullName, Password, createdRole, _session.AdminId);
            Username = FullName = Password = string.Empty;
            NewUserRole = "Employee";
            await LoadUsersAsync(); await LoadAuditAsync(); Announce($"{createdRole} account added successfully.");
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }
    private async Task ToggleUserAsync()
    {
        try
        {
            if (SelectedUser == null) throw new InvalidOperationException("Select a user account first.");
            if (SelectedUser.AdminId == _session.AdminId && SelectedUser.Status == "Active") throw new InvalidOperationException("You cannot deactivate the account you are using.");
            await _users.SetStatusAsync(SelectedUser.AdminId, SelectedUser.Status != "Active", _session.AdminId);
            await LoadUsersAsync(); await LoadAuditAsync(); Announce("Administrator access updated.");
        }
        catch (Exception ex) { SetError(ex.Message); }
    }
    private async Task ResetPasswordAsync()
    {
        try
        {
            if (SelectedUser == null) throw new InvalidOperationException("Select a user account first.");
            if (Password.Length < 10) throw new InvalidOperationException("Enter a new password with at least 10 characters.");
            await _users.ResetPasswordAsync(SelectedUser.AdminId, Password, _session.AdminId);
            Password = string.Empty; await LoadAuditAsync(); Announce("Administrator password reset.");
        }
        catch (Exception ex) { SetError(ex.Message); }
    }
}
