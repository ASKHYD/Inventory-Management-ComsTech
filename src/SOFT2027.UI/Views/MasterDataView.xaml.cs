using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace SOFT2027.UI.Views;

public partial class MasterDataView : UserControl
{
    public MasterDataView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is SOFT2027.Presentation.ViewModels.MasterDataViewModel viewModel && viewModel.LoadCommand.CanExecute(null))
                viewModel.LoadCommand.Execute(null);
        };
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
                MinWidth = 115
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
        e.Column.Width = DataGridLength.SizeToHeader;
    }
}
