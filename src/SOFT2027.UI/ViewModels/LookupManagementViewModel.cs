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
    public LookupManagementViewModel(IBusinessLookupRepository repository)
    {
        _repository = repository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AddCommand = new RelayCommand(() => { SelectedItem = new LookupItem { Name = "New name" }; Items.Add(SelectedItem); });
    }
    public string[] Kinds { get; } = ["Categories", "Brands", "Units"];
    public ObservableCollection<LookupItem> Items { get; } = new();
    public string Kind { get => _kind; set { if (SetProperty(ref _kind, value)) _ = LoadAsync(); } }
    public LookupItem? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand AddCommand { get; }
    public async Task LoadAsync()
    {
        try { Items.Clear(); foreach (var item in await _repository.GetAllAsync(Kind)) Items.Add(item); }
        catch (Exception ex) { SetError($"Could not load {Kind.ToLowerInvariant()}. {ex.Message}"); }
    }
    private async Task SaveAsync()
    {
        try { if (SelectedItem == null) throw new InvalidOperationException("Select or add a row before saving."); await _repository.SaveAsync(Kind, SelectedItem); await LoadAsync(); Announce($"{Kind} saved."); }
        catch (Exception ex) { SetError(ex.Message); }
    }
    public async Task DeleteSelectedAsync()
    {
        try { if (SelectedItem == null) throw new InvalidOperationException("Select a row to delete."); if (SelectedItem.Id == 0) { Items.Remove(SelectedItem); SelectedItem = null; return; } await _repository.DeleteAsync(Kind, SelectedItem.Id); await LoadAsync(); Announce("Item deleted."); }
        catch (Exception ex) { SetError(ex.Message); }
    }
}
