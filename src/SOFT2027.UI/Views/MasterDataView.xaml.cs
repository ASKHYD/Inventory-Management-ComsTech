using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SOFT2027.UI.Views;

public partial class MasterDataView : UserControl
{
    private bool _activationRequested;
    private SOFT2027.Presentation.ViewModels.MasterDataViewModel? _observedViewModel;

    public MasterDataView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ActivateViewAsync();
        IsVisibleChanged += async (_, e) =>
        {
            if (IsVisible) await ActivateViewAsync();
            else _activationRequested = false;
        };
        DataContextChanged += async (_, e) =>
        {
            _activationRequested = false;
            ObserveViewModel(e.NewValue as SOFT2027.Presentation.ViewModels.MasterDataViewModel);
            if (IsVisible) await ActivateViewAsync();
        };
        RecordsGrid.MouseDoubleClick += RecordsGrid_MouseDoubleClick;
    }

    private void ObserveViewModel(SOFT2027.Presentation.ViewModels.MasterDataViewModel? viewModel)
    {
        if (_observedViewModel != null)
        {
            _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _observedViewModel.Records.CollectionChanged -= Records_CollectionChanged;
        }
        _observedViewModel = viewModel;
        if (_observedViewModel != null)
        {
            _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _observedViewModel.Records.CollectionChanged += Records_CollectionChanged;
            ConfigureColumns(_observedViewModel.Title);
            UpdateEmptyState();
        }
    }

    private async Task ActivateViewAsync()
    {
        if (!IsVisible || _activationRequested || DataContext is not SOFT2027.Presentation.ViewModels.MasterDataViewModel vm) return;
        _activationRequested = true;
        LoadingStateText.Text = $"Loading {vm.Title.ToLowerInvariant()}…";
        LoadingStateBorder.Visibility = Visibility.Visible;
        try
        {
            if (vm.LoadCommand.CanExecute(null)) await vm.LoadCommand.ExecuteAsync(null);
        }
        finally
        {
            _activationRequested = false;
            UpdateEmptyState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SOFT2027.Presentation.ViewModels.MasterDataViewModel.IsBusy) or nameof(SOFT2027.Presentation.ViewModels.MasterDataViewModel.Title))
            UpdateEmptyState();
    }

    private void Records_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateEmptyState);
            return;
        }

        if (_observedViewModel == null) return;
        var busy = _observedViewModel.IsBusy || _activationRequested;
        var empty = !busy && _observedViewModel.FilteredRecords.IsEmpty;

        LoadingStateText.Text = $"Loading {_observedViewModel.Title.ToLowerInvariant()}…";
        LoadingStateBorder.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        EmptyStateText.Text = $"No {_observedViewModel.Title.ToLowerInvariant()} yet. Click Add {_observedViewModel.Title.TrimEnd('s')} to start.";
        EmptyStateBorder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfigureColumns(string section)
    {
        RecordsGrid.Columns.Clear();
        void Text(string header, string path, double minWidth = 90, DataGridLength? width = null)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(path),
                MinWidth = minWidth,
                Width = width ?? DataGridLength.Auto
            };
            if (path is "BasePurchasePrice" or "DefaultSalePrice" or "OpeningBalance" or "OutstandingBalance" or "CreditLimit")
                column.Binding = new System.Windows.Data.Binding(path) { StringFormat = "PKR {0:N2}" };
            if (path is "CurrentStock" or "MinimumStock")
                column.Binding = new System.Windows.Data.Binding(path) { StringFormat = "{0:N2}" };
            RecordsGrid.Columns.Add(column);
        }
        void Lookup(string header, string path, System.Collections.ObjectModel.ObservableCollection<SOFT2027.Core.Entities.LookupItem> items)
        {
            RecordsGrid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = header,
                ItemsSource = items,
                DisplayMemberPath = nameof(SOFT2027.Core.Entities.LookupItem.Name),
                SelectedValuePath = nameof(SOFT2027.Core.Entities.LookupItem.Id),
                SelectedValueBinding = new System.Windows.Data.Binding(path),
                MinWidth = 105,
                Width = DataGridLength.Auto
            });
        }
        void AddActionColumn()
        {
            var actionColumn = new DataGridTemplateColumn
            {
                Header = "Actions",
                Width = DataGridLength.Auto,
                MinWidth = 135
            };
            var cellTemplate = new DataTemplate();
            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var editBtn = new FrameworkElementFactory(typeof(Button));
            editBtn.SetValue(Button.ContentProperty, "Edit");
            editBtn.SetValue(Button.PaddingProperty, new Thickness(10, 3, 10, 3));
            editBtn.SetValue(Button.MarginProperty, new Thickness(2, 2, 4, 2));
            editBtn.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
            editBtn.SetValue(Button.FontSizeProperty, 11.5);
            editBtn.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
            editBtn.SetValue(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 242, 255)));
            editBtn.SetValue(Button.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 58, 138)));
            editBtn.SetValue(Button.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 210, 254)));
            editBtn.SetValue(Button.BorderThicknessProperty, new Thickness(1));
            editBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnEditRowButtonClicked));

            var deleteBtn = new FrameworkElementFactory(typeof(Button));
            deleteBtn.SetValue(Button.ContentProperty, "Delete");
            deleteBtn.SetValue(Button.PaddingProperty, new Thickness(10, 3, 10, 3));
            deleteBtn.SetValue(Button.MarginProperty, new Thickness(2, 2, 2, 2));
            deleteBtn.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
            deleteBtn.SetValue(Button.FontSizeProperty, 11.5);
            deleteBtn.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
            deleteBtn.SetValue(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242)));
            deleteBtn.SetValue(Button.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28)));
            deleteBtn.SetValue(Button.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 202, 202)));
            deleteBtn.SetValue(Button.BorderThicknessProperty, new Thickness(1));
            deleteBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnDeleteRowButtonClicked));

            panelFactory.AppendChild(editBtn);
            panelFactory.AppendChild(deleteBtn);
            cellTemplate.VisualTree = panelFactory;
            actionColumn.CellTemplate = cellTemplate;
            RecordsGrid.Columns.Add(actionColumn);
        }

        if (section == "Products")
        {
            Text("Code", "ProductCode", 85); Text("Barcode", "Barcode", 110); Text("Name", "ProductName", 180);
            Lookup("Category", "CategoryId", _observedViewModel!.Categories); Lookup("Brand", "BrandId", _observedViewModel.Brands); Lookup("Unit", "UnitId", _observedViewModel.Units);
            Text("Purchase Price", "BasePurchasePrice", 110); Text("Purchase %", "PurchasePercentage", 95); Text("Default Sale Price", "DefaultSalePrice", 125);
            Text("Selling %", "SellingPercentage", 95); Text("Current Stock", "CurrentStock", 100); Text("Min Stock", "MinimumStock", 90); Text("Status", "Status", 80);
            AddActionColumn();
        }
        else if (section == "Customers")
        {
            Text("Code", "CustomerCode", 85); Text("Customer Name", "CustomerName", 180);
            Text("Contact Person", "ContactPerson", 120); Text("Phone", "Phone", 115); Text("Address", "Address", 160);
            Text("Area / Territory", "AreaTerritory", 120); Text("Opening Balance", "OpeningBalance", 120); Text("Credit Limit", "CreditLimit", 110); Text("Status", "Status", 80);
            AddActionColumn();
        }
        else
        {
            Text("Code", "SupplierCode", 85); Text("Supplier Name", "SupplierName", 180);
            Text("Contact Person", "ContactPerson", 120); Text("Phone", "Phone", 115); Text("Address", "Address", 160);
            Text("Opening Balance", "OpeningBalance", 120); Text("Status", "Status", 80);
            AddActionColumn();
        }
    }

    private void OnEditRowButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext != null && DataContext is SOFT2027.Presentation.ViewModels.MasterDataViewModel vm)
        {
            vm.OpenEditForm(btn.DataContext);
        }
    }

    private async void OnDeleteRowButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext != null && DataContext is SOFT2027.Presentation.ViewModels.MasterDataViewModel vm)
        {
            await vm.InitiateDeleteForRecordAsync(btn.DataContext);
        }
    }

    private void RecordsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (RecordsGrid.SelectedItem != null && DataContext is SOFT2027.Presentation.ViewModels.MasterDataViewModel vm)
        {
            vm.OpenEditForm(RecordsGrid.SelectedItem);
        }
    }

    private void RecordsGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if ((e.PropertyName is "CategoryId" or "BrandId" or "UnitId") && DataContext is SOFT2027.Presentation.ViewModels.MasterDataViewModel viewModel)
        {
            var options = e.PropertyName switch
            {
                "CategoryId" => viewModel.Categories,
                "BrandId" => viewModel.Brands,
                _ => viewModel.Units
            };
            e.Column = new DataGridComboBoxColumn
            {
                Header = e.PropertyName.Replace("Id", string.Empty),
                ItemsSource = options,
                DisplayMemberPath = nameof(SOFT2027.Core.Entities.LookupItem.Name),
                SelectedValuePath = nameof(SOFT2027.Core.Entities.LookupItem.Id),
                SelectedValueBinding = new System.Windows.Data.Binding(e.PropertyName) { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged },
                MinWidth = 115,
                Width = DataGridLength.Auto
            };
            return;
        }
        if (e.PropertyName is "ProductId" or "CustomerId" or "SupplierId" or "CreatedAt" or "UpdatedAt")
        {
            e.Cancel = true;
            return;
        }

        if (e.PropertyName == "OutstandingBalance")
        {
            e.Cancel = true; // Updated by invoices, purchases, receipts, and payments.
            return;
        }
        if (e.PropertyName == "CurrentStock")
        {
            e.Column.IsReadOnly = true; // Stock must change through inventory transactions.
        }

        e.Column.Header = Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2");
        e.Column.MinWidth = 95;
        if (e.Column is DataGridTextColumn textColumn)
        {
            textColumn.ElementStyle = new Style(typeof(TextBlock));
            textColumn.EditingElementStyle = new Style(typeof(TextBox));
        }
        e.Column.Width = DataGridLength.Auto;
    }
}
