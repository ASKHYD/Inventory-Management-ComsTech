using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using SOFT2027.Presentation.ViewModels;
using SOFT2027.UI.Views;

namespace SOFT2027.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;
    private bool _tourShownThisSession;
    private MainViewModel? _observedViewModel;

    public MainWindow()
    {
        InitializeComponent();

        ClampToWorkArea();

        SourceInitialized += OnSourceInitialized;
        Loaded += MainWindow_Loaded;
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e) => ShowTourIfFirstRun();

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observedViewModel != null) _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _observedViewModel = e.NewValue as MainViewModel;
        if (_observedViewModel != null) _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ShowTourIfFirstRun();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAuthenticated))
            Dispatcher.BeginInvoke(ShowTourIfFirstRun);
    }

    private void ShowTourIfFirstRun()
    {
        if (_tourShownThisSession || _observedViewModel?.IsAuthenticated != true || App.HasCompletedTour()) return;
        _tourShownThisSession = true;
        ShowTour();
    }

    private void ShowTourButton_Click(object sender, RoutedEventArgs e) => ShowTour();

    private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainViewModel viewModel) return;
        var query = GlobalSearchBox.Text.Trim();
        var section = query.StartsWith("c:", StringComparison.OrdinalIgnoreCase) ? "Customers"
            : query.StartsWith("s:", StringComparison.OrdinalIgnoreCase) ? "Suppliers"
            : "Products";
        if (query.Length > 2 && query[1] == ':') query = query[2..].Trim();
        viewModel.NavigateToMasterData(section);
        if (viewModel.CurrentViewModel is MasterDataViewModel masterData)
            masterData.SearchText = query;
        e.Handled = true;
    }

    private void OpenInventoryFromNotifications_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel) viewModel.NavigateToInventory();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void ShowTour()
    {
        var tour = new AppTourWindow { Owner = this };
        var result = tour.ShowDialog();
        if (result.HasValue) App.MarkTourCompleted();
    }

    /// <summary>
    /// Ensures the custom-chrome window always opens fully on-screen and never
    /// larger than the usable desktop (fixes clipped sidebar / cut-off top bar
    /// that occurred when the hard-coded 1400x820 size exceeded the monitor).
    /// </summary>
    private void ClampToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;

        if (Width > workArea.Width)
        {
            Width = workArea.Width;
        }

        if (Height > workArea.Height)
        {
            Height = workArea.Height;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WindowChrome maximized windows otherwise extend past the monitor work
        // area, which clips the top bar / sidebar. Constrain to the work area.
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                var full = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.X = work.left - full.left;
                mmi.ptMaxPosition.Y = work.top - full.top;
                mmi.ptMaxSize.X = work.right - work.left;
                mmi.ptMaxSize.Y = work.bottom - work.top;
            }
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }
}
