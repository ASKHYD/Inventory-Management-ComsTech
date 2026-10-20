using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class LookupManagementViewModel : ViewModelBase
{
    private readonly IBusinessLookupRepository _repository;
    private string _kind = "Categories";
    private LookupItem? _selectedItem;
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private bool _isAddFormOpen;
    private string _newItemName = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                SaveNewItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public bool IsAddFormOpen
    {
        get => _isAddFormOpen;
        set => SetProperty(ref _isAddFormOpen, value);
    }

    public string NewItemName
    {
        get => _newItemName;
        set => SetProperty(ref _newItemName, value);
    }

    private string SingleKind => Kind switch
    {
        "Categories" => "Category",
        "Brands" => "Brand",
        "Units" => "Unit",
        _ => Kind.TrimEnd('s')
    };

    public string AddFormTitle => $"Add New {SingleKind}";
    public string AddButtonLabel => $"Add {SingleKind}";
    public string SaveNewActionLabel => $"Save {SingleKind}";

    public LookupManagementViewModel(IBusinessLookupRepository repository)
    {
        _repository = repository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        AddCommand = new RelayCommand(OpenAddForm);
        SaveNewItemCommand = new AsyncRelayCommand(SaveNewItemAsync, () => !IsBusy && IsAddFormOpen);
        CancelAddCommand = new RelayCommand(CancelAddForm);
    }

    public string[] Kinds { get; } = ["Categories", "Brands", "Units"];
    public ObservableCollection<LookupItem> Items { get; } = new();
    public string Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(AddFormTitle));
                OnPropertyChanged(nameof(AddButtonLabel));
                OnPropertyChanged(nameof(SaveNewActionLabel));
                _ = LoadAsync();
            }
        }
    }
    public LookupItem? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand AddCommand { get; }
    public AsyncRelayCommand SaveNewItemCommand { get; }
    public RelayCommand CancelAddCommand { get; }

    public void OpenAddForm()
    {
        ClearErrors();
        NewItemName = string.Empty;
        IsAddFormOpen = true;
        SaveNewItemCommand.RaiseCanExecuteChanged();
        Announce($"Opening {AddFormTitle} form.");
    }

    public void CancelAddForm()
    {
        IsAddFormOpen = false;
        NewItemName = string.Empty;
        ClearErrors();
        SaveNewItemCommand.RaiseCanExecuteChanged();
        Announce("Add operation cancelled.");
    }

    public async Task SaveNewItemAsync()
    {
        ClearErrors();
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            SetError($"Please enter a name for the new {SingleKind.ToLowerInvariant()}.");
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = $"Saving {SingleKind.ToLowerInvariant()}…";
            await Task.Yield();
            var newItem = new LookupItem { Name = NewItemName.Trim() };
            await _repository.SaveAsync(Kind, newItem);
            IsAddFormOpen = false;
            NewItemName = string.Empty;
            await LoadAsync();
            Announce($"{SingleKind} '{newItem.Name}' added successfully.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = $"Loading {Kind.ToLowerInvariant()}…";
            Items.Clear();
            foreach (var item in await _repository.GetAllAsync(Kind)) Items.Add(item);
            Announce($"{Kind} loaded.");
        }
        catch (Exception ex) { SetError($"Could not load {Kind.ToLowerInvariant()}. {ex.Message}"); }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (SelectedItem == null) throw new InvalidOperationException("Select or add a row before saving.");
            IsBusy = true;
            BusyMessage = $"Saving {Kind.ToLowerInvariant()}…";
            await _repository.SaveAsync(Kind, SelectedItem);
            await LoadAsync();
            Announce($"{Kind} saved.");
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    public async Task DeleteSelectedAsync()
    {
        try
        {
            if (SelectedItem == null) throw new InvalidOperationException("Select a row to delete.");
            if (SelectedItem.Id == 0) { Items.Remove(SelectedItem); SelectedItem = null; return; }
            IsBusy = true;
            BusyMessage = $"Deleting {Kind.TrimEnd('s').ToLowerInvariant()}…";
            await _repository.DeleteAsync(Kind, SelectedItem.Id);
            await LoadAsync();
            Announce("Item deleted.");
        }
        catch (Exception ex) { SetError(ex.Message); }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }
}
