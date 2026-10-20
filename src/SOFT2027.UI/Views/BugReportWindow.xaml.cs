using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SOFT2027.UI.BugReports;

namespace SOFT2027.UI.Views;

public partial class BugReportWindow : Window
{
    private readonly BugReportMetadata _metadata;
    private string? _savedFolder;
    private bool _closeConfirmed;

    public BugReportWindow(BugReportMetadata metadata)
    {
        InitializeComponent();
        _metadata = metadata;
        AppVersionValue.Text = metadata.AppVersion;
        WindowsVersionValue.Text = metadata.WindowsVersion;
        ModuleValue.Text = string.IsNullOrWhiteSpace(metadata.Module) ? "Unknown" : metadata.Module;
        DateValue.Text = metadata.ReportedAt.ToString("dd/MM/yyyy HH:mm");
        RoleValue.Text = metadata.Role;
        WhatsAppButton.Visibility = string.IsNullOrWhiteSpace(BugReportConfiguration.Destinations.WhatsAppNumber)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private BugReportInput? TryGetReport()
    {
        var titleValid = !string.IsNullOrWhiteSpace(TitleBox.Text);
        var happenedValid = !string.IsNullOrWhiteSpace(HappenedBox.Text);
        TitleError.Visibility = titleValid ? Visibility.Collapsed : Visibility.Visible;
        HappenedError.Visibility = happenedValid ? Visibility.Collapsed : Visibility.Visible;
        if (!titleValid || !happenedValid)
        {
            StatusText.Text = "Complete the required fields to prepare a report.";
            TitleBox.Focus();
            return null;
        }
        return new BugReportInput(TitleBox.Text.Trim(), HappenedBox.Text.Trim(), StepsBox.Text.Trim(), ExpectedBox.Text.Trim(),
            ((ComboBoxItem)SeverityBox.SelectedItem).Content.ToString()!, ((ComboBoxItem)RepeatableBox.SelectedItem).Content.ToString()!, _metadata);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var report = TryGetReport(); if (report is null) return;
        try { StatusText.Text = new CopyToClipboard().Deliver(report); }
        catch (Exception ex) { ShowDeliveryFailure("Could not copy the report", ex); }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var report = TryGetReport(); if (report is null) return;
        try
        {
            _savedFolder = new SaveToFile().Deliver(report);
            StatusText.Text = "Report saved in Documents\\SOFT2027 Reports. You can open the timestamped folder below.";
            OpenFolderButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { ShowDeliveryFailure("Could not save the report", ex); }
    }

    private void Mailto_Click(object sender, RoutedEventArgs e) => Send(BugReportRoute.Mailto, new MailtoDraft());
    private void Gmail_Click(object sender, RoutedEventArgs e) => Send(BugReportRoute.Gmail, new GmailWeb());
    private void WhatsApp_Click(object sender, RoutedEventArgs e) => Send(BugReportRoute.WhatsApp, new WhatsApp());

    private void Send(BugReportRoute route, IBugReportSender sender)
    {
        var report = TryGetReport(); if (report is null) return;
        try { StatusText.Text = sender.Deliver(report); }
        catch (Exception ex)
        {
            var routeName = route == BugReportRoute.Mailto ? "No email app is set up on this PC" : "Could not open the selected report destination";
            ShowDeliveryFailure(routeName, ex);
        }
    }

    private void ShowDeliveryFailure(string message, Exception ex)
    {
        StatusText.Text = $"{message}. Copy or save the report instead.";
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorTokenBrush");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_savedFolder)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_savedFolder}\"") { UseShellExecute = true }); }
        catch (Exception ex) { ShowDeliveryFailure("Could not open the report folder", ex); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeConfirmed || !HasUserContent()) return;
        var choice = MessageBox.Show(this, "Discard this bug report? It has not been sent.", "Close report",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (choice == MessageBoxResult.Yes) _closeConfirmed = true;
        else e.Cancel = true;
    }

    private bool HasUserContent() => !string.IsNullOrWhiteSpace(TitleBox.Text) ||
        !string.IsNullOrWhiteSpace(HappenedBox.Text) || !string.IsNullOrWhiteSpace(StepsBox.Text) ||
        !string.IsNullOrWhiteSpace(ExpectedBox.Text) || SeverityBox.SelectedIndex != 2 || RepeatableBox.SelectedIndex != 0;
}
