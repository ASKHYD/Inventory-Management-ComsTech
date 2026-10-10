using System.Windows.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using SOFT2027.Core.Entities;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views
{
    /// <summary>
    /// Interaction logic for ReportsDashboardView.xaml
    /// </summary>
    public partial class ReportsDashboardView : UserControl
    {
        public ReportsDashboardView()
        {
            InitializeComponent();
        }

        private async void ReportsDashboardView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReportsDashboardViewModel vm)
            {
                try
                {
                    await vm.InitializeReportFiltersAsync();
                    await vm.LoadReportsCommand.ExecuteAsync(null);
                }
                catch (Exception ex) { MessageBox.Show($"Report filters could not be loaded: {ex.Message}", "Reports", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
            RefreshOperationalReport();
        }

        private void ReportsDashboardView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ReportsDashboardViewModel oldVm) oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            if (e.NewValue is ReportsDashboardViewModel newVm) newVm.PropertyChanged += ViewModel_PropertyChanged;
            RefreshOperationalReport();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReportsDashboardViewModel.OperationalReport)) Dispatcher.Invoke(RefreshOperationalReport);
        }

        private void DashboardActivity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string module } || Window.GetWindow(this)?.DataContext is not MainViewModel main) return;
            switch (module)
            {
                case "Sales": main.NavigateToSalesCommand.Execute(null); break;
                case "Purchases": main.NavigateToPurchasesCommand.Execute(null); break;
                case "Receipts": main.NavigateToReceiptsCommand.Execute(null); break;
                case "Payments": main.NavigateToPaymentsCommand.Execute(null); break;
                case "Inventory": main.NavigateToInventoryCommand.Execute(null); break;
            }
        }

        private void RefreshOperationalReport()
        {
            OperationalReportGrid.Columns.Clear();
            if (DataContext is not ReportsDashboardViewModel vm || vm.OperationalReport is not { } report)
            {
                OperationalReportGrid.ItemsSource = null;
                ReportEmptyState.Text = "Run a report to see results.";
                ReportEmptyState.Visibility = Visibility.Visible;
                return;
            }
            foreach (var column in report.Columns)
            {
                var binding = new Binding($"Values[{column.Key}]") { Mode = BindingMode.OneWay, StringFormat = column.Format == "d" ? "d" : column.Format == null ? null : "{0:" + column.Format + "}" };
                OperationalReportGrid.Columns.Add(new DataGridTextColumn { Header = column.Header, Binding = binding, Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true });
            }
            OperationalReportGrid.ItemsSource = report.Rows;
            ReportEmptyState.Text = report.Rows.Count == 0 ? "No rows match the selected filters." : $"{report.Rows.Count} row(s)";
            ReportEmptyState.Visibility = Visibility.Visible;
        }

        private void ExportOperationalReport_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ReportsDashboardViewModel { OperationalReport: { } report } || report.Columns.Count == 0) return;
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = string.Concat(report.Title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + ".csv", AddExtension = true, DefaultExt = ".csv" };
            if (dialog.ShowDialog() != true) return;
            static string Csv(string? text) => "\"" + (text ?? string.Empty).Replace("\"", "\"\"") + "\"";
            try
            {
                using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", report.Columns.Select(c => Csv(c.Header))));
                foreach (var row in report.Rows)
                    writer.WriteLine(string.Join(",", report.Columns.Select(c => Csv(Format(row.Values.TryGetValue(c.Key, out var value) ? value : null, c.Format)))));
                MessageBox.Show("Report exported successfully.", "Export report", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"The report could not be exported: {ex.Message}", "Export report", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static string Format(object? value, string? format)
        {
            if (value == null || value is DBNull) return string.Empty;
            if (value is IFormattable formattable) return formattable.ToString(format == "d" ? "d" : format, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
            return value.ToString() ?? string.Empty;
        }

    }
}
