using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using SOFT2027.UI;
using SOFT2027.UI.Views;
using SOFT2027.UI.Converters;
using SOFT2027.UI.BugReports;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr extraData);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, System.Text.StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsCallback callback, IntPtr extraData);
    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr extraData);

    private static readonly List<string> Failures = [];
    private static int DataGridHeaderCount;
    private static readonly List<(string Control, string Foreground, string Background, double Ratio)> ContrastRows = [];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--startup-probe") return ProbeApplicationStartup(args[1]);

        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            app.InitializeComponent();
            Console.WriteLine($"PASS App resources ({app.Resources.MergedDictionaries.Count} merged dictionaries)");
            CheckColorResourceTypes();
        }
        catch (Exception ex)
        {
            ReportFailure("App resources", ex);
            return 1;
        }

        Check("MainWindow / Login shell", () => new MainWindow());
        CheckWindowControlStates();
        Check("Login", () => new LoginView());
        Check("Sales Entry", () => new SalesEntryView());
        Check("Purchase Entry", () => new PurchaseEntryView());
        Check("Returns", () => new ReturnEntryView());
        Check("Inventory", () => new InventoryView());
        Check("Products, Customers, Suppliers", () => new MasterDataView());
        Check("Categories, Brands, Units", () => new LookupManagementView());
        Check("Receipts, Payments, Expenses", () => new FinancialEntryView());
        Check("Cash & Bank", () => new CashBankView());
        Check("Customer and Supplier Ledgers", () => new LedgerView());
        Check("Reports", () => new ReportsDashboardView());
        Check("Backup and Restore", () => new BackupView());
        Check("Administration and Activity", () => new AdministrationView());
        Check("Help and Tour", () => new AppTourWindow());
        Check("Bug report dialog", () => new BugReportWindow(new BugReportMetadata("QA smoke", "Windows QA", "QA module", DateTimeOffset.Now, "QA role")));
        ReproduceMasterDataInitialLoads();
        CheckBugReportLayoutAt1366x768();
        CheckBugReportUrlBuilder();

        var sample = new TransactionDocumentData
        {
            Title = "QA Preview", Number = "QA-PREVIEW-001", Date = new DateTime(2026, 10, 13),
            PartyLabel = "Customer", PartyName = "QA-Preview-Party", PartyCode = "QA-CUST-001",
            Lines = new[] { new TransactionDocumentLine { ProductCode = "QA-PROD-001", ProductName = "QA Preview Product", Quantity = 1, UnitPrice = 100, LineTotal = 100 } },
            SubTotal = 100, Total = 100
        };
        Check("Single memo preview (code-built UI)", () => new TransactionDocumentPreviewWindow(sample));
        Check("Four-up memo preview (code-built UI)", () => new TransactionDocumentPreviewWindow(new[] { sample }));
        CheckFourUpCellGeometry(sample);
        CheckExistingPreviewZoomAndCapture(sample);
        GeneratePreviewSamples(sample);
        CheckRenderedStateProbes();

        Console.WriteLine("\nRendered contrast table (effective WPF TextBlock Foreground against rendered control surface):");
        Console.WriteLine("Control | effective foreground | background | WCAG ratio");
        foreach (var row in ContrastRows)
            Console.WriteLine($"{row.Control} | {row.Foreground} | {row.Background} | {row.Ratio:F2}:1");

        if (DataGridHeaderCount == 0)
            ReportFailure("DataGrid header audit", new InvalidOperationException("No rendered DataGrid headers were inspected."));
        if (Failures.Count > 0)
        {
            Console.Error.WriteLine($"Rendered XAML smoke check failed: {Failures.Count} failure(s).");
            return 1;
        }
        Console.WriteLine($"PASS rendered DataGrid header contrast ({DataGridHeaderCount} headers; all >= 4.5:1)");
        Console.WriteLine("PASS rendered state probes (selected row, disabled button, tabs, sidebar, tooltip, placeholder)");
        Console.WriteLine("PASS XAML smoke check: all registered view roots instantiated and rendered.");
        return 0;
    }

    private static int ProbeApplicationStartup(string executable)
    {
        Process? application = null;
        try
        {
            application = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true })
                ?? throw new InvalidOperationException("Windows did not start the QA application process.");
            var timeout = Stopwatch.StartNew();
            var discoveredTitles = new HashSet<string>(StringComparer.Ordinal);
            while (timeout.Elapsed < TimeSpan.FromSeconds(25) && !application.HasExited)
            {
                string? startupError = null;
                string? mainWindow = null;
                EnumWindows((handle, _) =>
                {
                    GetWindowThreadProcessId(handle, out var processId);
                    if (processId != application.Id || !IsWindowVisible(handle)) return true;
                    var title = new System.Text.StringBuilder(512);
                    GetWindowText(handle, title, title.Capacity);
                    var value = title.ToString();
                    if (value.Length == 0) return true;
                    discoveredTitles.Add(value);
                    if (value.Contains("Startup Error", StringComparison.OrdinalIgnoreCase)) startupError = value;
                    if (value.StartsWith("SOFT2027 — Ali Traders", StringComparison.Ordinal)) mainWindow = value;
                    return true;
                }, IntPtr.Zero);

                if (startupError != null)
                {
                    var message = ReadWindowText(startupError, application.Id);
                    Console.Error.WriteLine($"FAIL QA startup probe: unexpected window '{startupError}'. Message: {message}");
                    StopProbeProcess(application);
                    return 1;
                }
                if (mainWindow != null)
                {
                    Console.WriteLine($"PASS QA startup probe: login shell appeared as '{mainWindow}'.");
                    StopProbeProcess(application);
                    return 0;
                }
                Thread.Sleep(200);
                application.Refresh();
            }

            var detail = application.HasExited
                ? $"process exited with code {application.ExitCode}"
                : "login shell did not appear within 25 seconds";
            Console.Error.WriteLine($"FAIL QA startup probe: {detail}. Visible window titles: {string.Join(" | ", discoveredTitles)}");
            StopProbeProcess(application);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL QA startup probe: {ex}");
            if (application != null) StopProbeProcess(application);
            return 1;
        }
    }

    private static string ReadWindowText(string title, int processId)
    {
        var texts = new List<string> { title };
        IntPtr top = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var owner);
            var caption = new System.Text.StringBuilder(512);
            GetWindowText(handle, caption, caption.Capacity);
            if (owner == processId && caption.ToString() == title) { top = handle; return false; }
            return true;
        }, IntPtr.Zero);
        if (top == IntPtr.Zero) return title;
        EnumChildWindows(top, (handle, _) =>
        {
            var text = new System.Text.StringBuilder(2048);
            GetWindowText(handle, text, text.Capacity);
            var value = text.ToString().Trim();
            if (value.Length > 0 && !texts.Contains(value, StringComparer.Ordinal)) texts.Add(value);
            return true;
        }, IntPtr.Zero);
        return string.Join(" | ", texts);
    }

    private static void StopProbeProcess(Process application)
    {
        try
        {
            if (!application.HasExited) application.Kill(entireProcessTree: true);
            application.WaitForExit(5000);
        }
        catch { }
    }

    private static void CheckExistingPreviewZoomAndCapture(TransactionDocumentData sample)
    {
        TransactionDocumentPreviewWindow? preview = null;
        try
        {
            preview = new TransactionDocumentPreviewWindow(sample)
            {
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Opacity = 1,
                Width = 1080,
                Height = 820
            };
            preview.Show();
            preview.UpdateLayout();
            PumpLayout(preview);
            var host = VisualAndLogicalDescendants(preview).OfType<Border>().FirstOrDefault(element => Equals(element.Tag, "PreviewPageHost"))
                ?? throw new InvalidOperationException("The rendered fixed-page host was not found.");
            var printPageSize = preview.PreviewDocument.DocumentPaginator.PageSize;
            preview.SetZoomPercent(50);
            preview.UpdateLayout();
            PumpLayout(preview);
            var samplesDirectory = Path.Combine(FindProjectRoot(), "qa-artifacts", "preview-samples");
            Directory.CreateDirectory(samplesDirectory);
            var shot = new RenderTargetBitmap(1080, 820, 96, 96, PixelFormats.Pbgra32);
            shot.Render(preview);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(shot));
            using (var stream = File.Create(Path.Combine(samplesDirectory, "preview-window.png"))) encoder.Save(stream);
            var measured = new List<(double Zoom, Rect Bounds)>();
            foreach (var zoom in new[] { 50d, 100d })
            {
                preview.SetZoomPercent(zoom);
                preview.UpdateLayout();
                PumpLayout(preview);
                var bounds = host.TransformToAncestor(preview).TransformBounds(new Rect(0, 0, host.ActualWidth, host.ActualHeight));
                Console.WriteLine($"Fixed-page zoom state: requested={zoom}; host={host.ActualWidth:F0}x{host.ActualHeight:F0}; rendered bounds={bounds.Width:F0}x{bounds.Height:F0}");
                measured.Add((zoom, bounds));
                if (preview.PreviewDocument.DocumentPaginator.PageSize != printPageSize)
                    throw new InvalidOperationException("Screen zoom changed the print paginator page size.");
            }
            var low = measured[0];
            var high = measured[1];
            var ratio = high.Bounds.Width / low.Bounds.Width;
            Console.WriteLine($"FIXED-PAGE PREVIEW ZOOM MEASUREMENT: Zoom={low.Zoom}% page bounds={low.Bounds.Width:F0}x{low.Bounds.Height:F0}px; Zoom={high.Zoom}% page bounds={high.Bounds.Width:F0}x{high.Bounds.Height:F0}px; width ratio={ratio:F2}x");
            if (high.Bounds.Width <= low.Bounds.Width || ratio < 1.2)
                ReportFailure("Preview zoom behavior", new InvalidOperationException($"Rendered page width did not scale meaningfully from 50% to 100% (ratio {ratio:F2}x)."));
            else Console.WriteLine("PASS preview zoom changes rendered page size");
        }
        catch (Exception ex) { ReportFailure("Current preview zoom render measurement", ex); }
        finally { try { preview?.Close(); } catch { } }
    }

    private static void GeneratePreviewSamples(TransactionDocumentData baseline)
    {
        try
        {
            var folder = Path.Combine(FindProjectRoot(), "qa-artifacts", "preview-samples");
            Directory.CreateDirectory(folder);
            var samples = new Dictionary<string, TransactionDocumentData>
            {
                ["single-1-line"] = WithLines(baseline, 1),
                ["single-12-lines"] = WithLines(baseline, 12),
                ["single-40-lines"] = WithLines(baseline, 40),
                ["single-long-names"] = WithLines(baseline, 1, longNames: true),
                ["single-urdu"] = WithLines(baseline, 1, urdu: true),
                ["single-zero-discount"] = WithLines(baseline, 1, zeroDiscount: true)
            };
            foreach (var (name, data) in samples)
            {
                var document = TransactionDocumentPreviewWindow.CreateSinglePreviewDocument(data);
                RenderPages(document, folder, name);
            }
            var sheet = TransactionDocumentPreviewWindow.CreateFourUpPreviewDocument(new[] { WithLines(baseline, 40) });
            RenderPages(sheet, folder, "four-up-40-lines");
            var blankCellSheet = TransactionDocumentPreviewWindow.CreateFourUpPreviewDocument(new[] { WithLines(baseline, 1) });
            RenderPages(blankCellSheet, folder, "four-up-1-line");
            Console.WriteLine($"PASS rendered preview samples to {folder}");
        }
        catch (Exception ex) { ReportFailure("Preview PNG samples", ex); }
    }

    private static void CheckFourUpCellGeometry(TransactionDocumentData sample)
    {
        try
        {
            var document = TransactionDocumentPreviewWindow.CreateFourUpPreviewDocument(new[] { sample });
            var page = document.Pages[0].Child as FixedPage ?? throw new InvalidOperationException("4-up first page is not a FixedPage.");
            var cells = VisualDescendants(page).OfType<System.Windows.Shapes.Rectangle>()
                .Where(rectangle => Math.Abs(rectangle.Width - 377.85) < 1 && Math.Abs(rectangle.Height - 542.25) < 1)
                .ToArray();
            if (cells.Length != 4) throw new InvalidOperationException($"Expected four equal fixed cells, found {cells.Length}.");
            Console.WriteLine("PASS four-up cell geometry (four equal fixed A4 cells; unused slots stay present)");
        }
        catch (Exception ex) { ReportFailure("Four-up equal-cell layout", ex); }
    }

    private static void ReproduceMasterDataInitialLoads()
    {
        var source = Path.Combine(FindProjectRoot(), "qa-artifacts", "SOFT2027-QA-Dummy-Data-Backup-20261013.db");
        var folder = Path.Combine(FindProjectRoot(), "qa-artifacts", "data-loading-smoke");
        var copy = Path.Combine(folder, "SOFT2027-QA-data-loading-copy.db");
        Directory.CreateDirectory(folder);
        if (!File.Exists(source))
        {
            if (File.Exists(copy))
            {
                try { File.Delete(copy); } catch { }
            }
            // Self-contained initialization: create clean synthetic QA database if pre-seeded backup file is not present
            var initContainer = Bootstrapper.CreateContainer($"Data Source={copy};Foreign Keys=True;");
            var initializer = initContainer.GetRequiredService<IDatabaseInitializer>();
            initializer.InitializeAsync().GetAwaiter().GetResult();
            var factory = initContainer.GetRequiredService<ISqliteConnectionFactory>();
            using var initConn = factory.CreateConnection();
            initConn.Open();
            using (var cmd = initConn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO AdminUsers (AdminId, Username, PasswordHash, FullName, Status, Role, CreatedAt)
                    VALUES (1, 'qa.admin', 'hash123', 'QA Administrator', 'Active', 'Administrator', CURRENT_TIMESTAMP);
                    INSERT OR REPLACE INTO Customers (CustomerId, CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory, OpeningBalance, OutstandingBalance, CreditLimit, Status, CreatedAt, UpdatedAt)
                    VALUES (1, 'CUST-001', 'Al-Madina Traders', 'Muhammad Ali', '03001234567', 'Market Road', 'Central', 0.0, 0.0, 100000.0, 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    INSERT OR REPLACE INTO Suppliers (SupplierId, SupplierCode, SupplierName, ContactPerson, Phone, Address, OpeningBalance, OutstandingBalance, Status, CreatedAt, UpdatedAt)
                    VALUES (1, 'SUPP-001', 'National Foods Ltd', 'Tariq Mehmood', '03211234567', 'Industrial Area, Karachi', 0.0, 0.0, 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    INSERT OR REPLACE INTO Categories (CategoryId, CategoryName, Description)
                    VALUES (1, 'Grains & Pulses', 'Bulk grains and pulses');
                    INSERT OR REPLACE INTO Products (ProductId, ProductCode, ProductName, CategoryId, BasePurchasePrice, DefaultSalePrice, CurrentStock, MinimumStock, Status, CreatedAt, UpdatedAt)
                    VALUES (101, 'PROD-101', 'Basmati Rice 25kg', 1, 100.0, 150.0, 20.0, 5.0, 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                    INSERT OR REPLACE INTO AuditLogs (AuditId, AdminId, Action, ModuleName, RecordReference, Timestamp)
                    VALUES (1, 1, 'CREATE', 'Customers', 'CUST-001', CURRENT_TIMESTAMP);
                ";
                cmd.ExecuteNonQuery();
            }
        }
        else
        {
            File.Copy(source, copy, overwrite: true);
        }

        try
        {
            // Use only a disposable copy of the seeded QA database.
            var container = Bootstrapper.CreateContainer($"Data Source={copy};Foreign Keys=True;");
            var session = container.GetRequiredService<IAdminSession>();
            session.SignIn(new AdminUser { AdminId = 1, Username = "qa.admin", FullName = "QA Administrator", Role = "Administrator" });
            var products = container.GetRequiredService<IProductRepository>();
            var customers = container.GetRequiredService<ICustomerRepository>();
            var suppliers = container.GetRequiredService<ISupplierRepository>();
            var lookups = container.GetRequiredService<IBusinessLookupRepository>();
            var audit = container.GetRequiredService<IAuditLogRepository>();
            var users = container.GetRequiredService<IAdminUserRepository>();

            foreach (var section in new[] { "Customers", "Suppliers", "Products" })
            {
                var vm = new SOFT2027.Presentation.ViewModels.MasterDataViewModel(section, products, customers, suppliers, lookups, audit, session);
                var view = new MasterDataView { DataContext = vm };
                var host = ShowForLoad(view);
                PumpUntil(host, () => !vm.LoadCommand.IsExecuting && vm.Records.Count > 0, TimeSpan.FromSeconds(8));
                var grid = VisualAndLogicalDescendants(view).OfType<DataGrid>().FirstOrDefault();
                ReportLoadState(section, vm.Records.Count, grid?.Columns.Count ?? 0, !vm.LoadCommand.IsExecuting,
                    vm.Records.Count > 0 && (grid?.Columns.Count ?? 0) > 0);
                if (section == "Customers")
                {
                    view.Visibility = Visibility.Collapsed;
                    vm.Records.Clear();
                    view.Visibility = Visibility.Visible;
                    PumpUntil(host, () => vm.Records.Count > 0, TimeSpan.FromSeconds(8));
                    ReportLoadState("Customers / hide and show again", vm.Records.Count, grid?.Columns.Count ?? 0, true,
                        vm.Records.Count > 0 && (grid?.Columns.Count ?? 0) > 0);
                }
                host.Close();
            }

            var lookupVm = new SOFT2027.Presentation.ViewModels.LookupManagementViewModel(lookups);
            var lookupView = new LookupManagementView { DataContext = lookupVm };
            var lookupHost = ShowForLoad(lookupView);
            PumpUntil(lookupHost, () => lookupVm.Items.Count > 0, TimeSpan.FromSeconds(8));
            var lookupGrid = VisualAndLogicalDescendants(lookupView).OfType<DataGrid>().FirstOrDefault();
            ReportLoadState("Lookups / Categories", lookupVm.Items.Count, lookupGrid?.Columns.Count ?? 0, true,
                lookupVm.Items.Count > 0 && (lookupGrid?.Columns.Count ?? 0) > 0);
            lookupHost.Close();

            var adminVm = new SOFT2027.Presentation.ViewModels.AdministrationViewModel(users, audit, session);
            var adminView = new AdministrationView { DataContext = adminVm };
            var adminHost = ShowForLoad(adminView);
            PumpUntil(adminHost, () => adminVm.Users.Count > 0, TimeSpan.FromSeconds(8));
            var usersGrid = VisualAndLogicalDescendants(adminView).OfType<DataGrid>()
                .FirstOrDefault(grid => ReferenceEquals(grid.ItemsSource, adminVm.Users));
            ReportLoadState("Administration / User Accounts", adminVm.Users.Count, usersGrid?.Columns.Count ?? 0, true,
                adminVm.Users.Count > 0 && (usersGrid?.Columns.Count ?? 0) > 0);
            var tabs = VisualAndLogicalDescendants(adminView).OfType<TabControl>().First();
            tabs.SelectedIndex = 1;
            PumpUntil(adminHost, () => adminVm.AuditEntries.Count > 0, TimeSpan.FromSeconds(8));
            var activityGrid = VisualAndLogicalDescendants(adminView).OfType<DataGrid>()
                .FirstOrDefault(grid => ReferenceEquals(grid.ItemsSource, adminVm.AuditEntries));
            ReportLoadState("Administration / Activity Log", adminVm.AuditEntries.Count, activityGrid?.Columns.Count ?? 0, true,
                adminVm.AuditEntries.Count > 0 && (activityGrid?.Columns.Count ?? 0) > 0);
            adminHost.Close();

            // Mimic the ContentPresenter reusing the same DataTemplate root as navigation
            // moves away and then returns to Customers with a fresh view model.
            var revisitVm = new SOFT2027.Presentation.ViewModels.MasterDataViewModel("Customers", products, customers, suppliers, lookups, audit, session);
            var revisitView = new MasterDataView();
            var revisitHost = ShowForLoad(revisitView);
            revisitView.DataContext = revisitVm;
            PumpUntil(revisitHost, () => !revisitVm.LoadCommand.IsExecuting && revisitVm.Records.Count > 0, TimeSpan.FromSeconds(8));
            var revisitGrid = VisualAndLogicalDescendants(revisitView).OfType<DataGrid>().FirstOrDefault();
            ReportLoadState("Customers / navigate away and back", revisitVm.Records.Count, revisitGrid?.Columns.Count ?? 0, true,
                revisitVm.Records.Count > 0 && (revisitGrid?.Columns.Count ?? 0) > 0);
            revisitHost.Close();

            Console.WriteLine($"QA-only seeded database copy used: {copy}");
            CaptureAddModalsAndLoadingEvidence(products, customers, suppliers, lookups, audit, session);
        }
        catch (Exception ex) { ReportFailure("Seeded database view-load reproduction", ex); }
    }

    private static void CaptureAddModalsAndLoadingEvidence(IProductRepository products, ICustomerRepository customers, ISupplierRepository suppliers, IBusinessLookupRepository lookups, IAuditLogRepository audit, IAdminSession session)
    {
        try
        {
            var evidenceDir = Path.Combine(FindProjectRoot(), "qa-artifacts", "live-run-evidence");
            Directory.CreateDirectory(evidenceDir);

            // 1. Capture Product Add Modal
            var masterVm = new SOFT2027.Presentation.ViewModels.MasterDataViewModel("Products", products, customers, suppliers, lookups, audit, session);
            var masterView = new MasterDataView { DataContext = masterVm };
            var masterHost = ShowForLoad(masterView);
            PumpUntil(masterHost, () => !masterVm.LoadCommand.IsExecuting && masterVm.Records.Count > 0, TimeSpan.FromSeconds(8));
            
            // Open Add Modal
            masterVm.AddCommand.Execute(null);
            masterHost.UpdateLayout();
            PumpLayout(masterHost);
            SaveWindowScreenshot(masterHost, Path.Combine(evidenceDir, "06_live_product_add_modal.png"));
            Console.WriteLine("PASS captured 06_live_product_add_modal.png");

            // 2. Capture Validation Failure with Form Remaining Open
            if (masterVm.NewRecordDraft is Product p) p.ProductName = "";
            masterVm.SaveNewRecordCommand.Execute(null);
            masterHost.UpdateLayout();
            PumpLayout(masterHost);
            if (!masterVm.IsAddFormOpen) throw new InvalidOperationException("Form closed on validation error!");
            SaveWindowScreenshot(masterHost, Path.Combine(evidenceDir, "07_live_add_validation_error.png"));
            Console.WriteLine("PASS captured 07_live_add_validation_error.png");

            // Close form
            masterVm.CancelAddCommand.Execute(null);
            masterHost.Close();

            // 3. Capture Lookup Add Modal (Categories)
            var lookupVm = new SOFT2027.Presentation.ViewModels.LookupManagementViewModel(lookups);
            var lookupView = new LookupManagementView { DataContext = lookupVm };
            var lookupHost = ShowForLoad(lookupView);
            PumpUntil(lookupHost, () => lookupVm.Items.Count > 0, TimeSpan.FromSeconds(8));

            lookupVm.AddCommand.Execute(null);
            lookupHost.UpdateLayout();
            PumpLayout(lookupHost);
            SaveWindowScreenshot(lookupHost, Path.Combine(evidenceDir, "08_live_lookup_add_modal.png"));
            Console.WriteLine("PASS captured 08_live_lookup_add_modal.png");
            lookupHost.Close();

            // 4. Capture Loading Spinner & Busy State
            var busyVm = new SOFT2027.Presentation.ViewModels.MasterDataViewModel("Products", products, customers, suppliers, lookups, audit, session);
            var busyView = new MasterDataView { DataContext = busyVm };
            var busyHost = ShowForLoad(busyView);
            busyVm.IsBusy = true;
            busyVm.StatusAnnouncement = "Loading products…";
            busyHost.UpdateLayout();
            PumpLayout(busyHost);
            SaveWindowScreenshot(busyHost, Path.Combine(evidenceDir, "09_live_loading_spinner_active.png"));
            Console.WriteLine("PASS captured 09_live_loading_spinner_active.png");
            busyHost.Close();
        }
        catch (Exception ex)
        {
            ReportFailure("Visual Add Modal & Spinner capture", ex);
        }
    }

    private static void SaveWindowScreenshot(Window window, string outputPath)
    {
        var prevOpacity = window.Opacity;
        window.Opacity = 1;
        window.UpdateLayout();
        PumpLayout(window);
        var width = (int)Math.Max(window.ActualWidth, 1200);
        var height = (int)Math.Max(window.ActualHeight, 800);
        var visual = (Visual)window.Content;
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        // Draw white background then visual
        var drawingVisual = new DrawingVisual();
        using (var ctx = drawingVisual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            ctx.DrawRectangle(new VisualBrush(visual) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, width, height));
        }
        rtb.Render(drawingVisual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        window.Opacity = prevOpacity;
    }

    private static Window ShowForLoad(FrameworkElement view)
    {
        var window = new Window { Content = view, Width = 1500, Height = 950, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Opacity = 0 };
        window.Show();
        window.UpdateLayout();
        PumpLayout(window);
        return window;
    }

    private static void PumpUntil(Window window, Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
            window.UpdateLayout();
        }
    }

    private static void ReportLoadState(string name, int rows, int headers, bool settled, bool pass)
    {
        Console.WriteLine($"{(pass ? "PASS" : "FAIL")} seeded load without Refresh: {name}; rows={rows}; headers={headers}; settled={settled}");
        if (!pass) ReportFailure($"Initial seeded load: {name}", new InvalidOperationException("Expected seeded rows and DataGrid headers without clicking Refresh."));
    }

    private static TransactionDocumentData WithLines(TransactionDocumentData baseline, int count, bool longNames = false, bool urdu = false, bool zeroDiscount = false)
    {
        var lines = Enumerable.Range(1, count).Select(index => new TransactionDocumentLine
        {
            ProductCode = $"QA-P{index:000}",
            ProductName = longNames ? "QA Long Name Product " + string.Concat(Enumerable.Repeat("Wide Pack Size ", 9)) : urdu ? "QA اردو ٹیسٹ پروڈکٹ پیک" : $"QA Dummy Product {index:000} Pack 12 x 1",
            Quantity = index,
            UnitPrice = 125.50m,
            DiscountPercentage = zeroDiscount ? 0m : 5m,
            LineTotal = index * 125.50m * (zeroDiscount ? 1m : .95m)
        }).ToArray();
        var gross = lines.Sum(line => line.Quantity * line.UnitPrice);
        var total = lines.Sum(line => line.LineTotal);
        return new TransactionDocumentData
        {
            Title = "Cash Memo", Number = "QA-SAL-20261013-001", Date = new DateTime(2026, 10, 13),
            PartyLabel = "Customer", PartyName = longNames ? "QA-Customer With A Deliberately Long Name" : "QA-Customer-001",
            PartyCode = "QA-CUST-001", AreaTerritory = "QA Area 01", Phone = "QA-000-0000",
            Lines = lines, SubTotal = gross, Discount = zeroDiscount ? 0m : gross - total,
            Total = total, Paid = total, Balance = 0m,
            Remarks = baseline.Remarks
        };
    }

    private static void RenderPages(FixedDocument document, string folder, string stem)
    {
        var paginator = document.DocumentPaginator;
        paginator.PageSize = new Size(793.7, 1122.5);
        paginator.ComputePageCount();
        for (var pageIndex = 0; pageIndex < paginator.PageCount; pageIndex++)
        {
            var page = paginator.GetPage(pageIndex);
            var bitmap = new RenderTargetBitmap(1240, 1754, 150, 150, PixelFormats.Pbgra32);
            bitmap.Render(page.Visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var suffix = paginator.PageCount > 1 ? $"-page-{pageIndex + 1}" : string.Empty;
            using var stream = File.Create(Path.Combine(folder, $"{stem}{suffix}.png"));
            encoder.Save(stream);
        }
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SOFT2027.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("SOFT2027.sln was not found above the smoke utility.");
    }

    private static void CheckBugReportLayoutAt1366x768()
    {
        BugReportWindow? dialog = null;
        try
        {
            dialog = new BugReportWindow(new BugReportMetadata("QA smoke", "Windows QA", "QA module", DateTimeOffset.Now, "QA role"))
            {
                Width = 1366,
                Height = 768,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
                ShowInTaskbar = false,
                Opacity = 0
            };
            var close = dialog.FindName("CloseButton") as Button;
            var actions = dialog.FindName("ActionBar") as FrameworkElement;
            if (close is null || close.Visibility != Visibility.Visible)
                throw new InvalidOperationException("The visible Close X button is missing from the dialog.");
            if ((string?)ToolTipService.GetToolTip(close) != "Close" || AutomationProperties.GetName(close) != "Close")
                throw new InvalidOperationException("The Close button tooltip or automation name is incorrect.");
            dialog.Show();
            dialog.UpdateLayout();
            PumpLayout(dialog);
            if (actions is null || actions.ActualHeight <= 0)
                throw new InvalidOperationException("The fixed bottom action bar did not render.");
            var bounds = actions.TransformToAncestor(dialog).TransformBounds(new Rect(new Point(0, 0), actions.RenderSize));
            if (bounds.Top < 0 || bounds.Bottom > dialog.ActualHeight + 1 || bounds.Left < 0 || bounds.Right > dialog.ActualWidth + 1)
                throw new InvalidOperationException($"Action bar bounds {bounds} fall outside the 1366x768 test window ({dialog.ActualWidth}x{dialog.ActualHeight}).");
            Console.WriteLine($"PASS bug report close/action bar bounds at 1366x768 (window rendered {dialog.ActualWidth:F0}x{dialog.ActualHeight:F0})");
        }
        catch (Exception ex) { ReportFailure("Bug report 1366x768 layout", ex); }
        finally { try { dialog?.Close(); } catch { } }
    }

    private static void CheckBugReportUrlBuilder()
    {
        var metadata = new BugReportMetadata("1.0 QA", "Windows QA", "QA module", DateTimeOffset.Parse("2026-10-13T12:00:00+05:00"), "Employee");
        var input = new BugReportInput("Title & ? # + =", "Line one\r\nLine two — اردو", "Step 1\nStep 2", "Expected output", "Minor", "Sometimes", metadata);
        CheckUrl(BugReportRoute.Mailto, input, null, "special chars and line breaks");
        CheckUrl(BugReportRoute.Gmail, input, null, "Gmail recipient and Urdu text");
        CheckUrl(BugReportRoute.WhatsApp, input, "15551234567", "WhatsApp phone and Urdu text");
        var longInput = input with { Happened = string.Concat(Enumerable.Repeat("Long Urdu رپورٹ text & special chars\r\n", 500)) };
        foreach (var route in new[] { BugReportRoute.Mailto, BugReportRoute.Gmail, BugReportRoute.WhatsApp })
        {
            var url = BugReportUrlBuilder.Build(route, longInput, "15551234567");
            if (url.Length > BugReportUrlBuilder.MaximumUrlLength || !url.Contains("Report%20shortened%20for%20this%20link", StringComparison.Ordinal))
                ReportFailure($"{route} long URL", new InvalidOperationException($"URL length={url.Length}; expected <= {BugReportUrlBuilder.MaximumUrlLength} and a shortening notice."));
            else Console.WriteLine($"PASS {route} URL length limit / long text ({url.Length} chars)");
        }
    }

    private static void CheckUrl(BugReportRoute route, BugReportInput input, string? phone, string description)
    {
        try
        {
            var url = BugReportUrlBuilder.Build(route, input, phone);
            if (url.Length > BugReportUrlBuilder.MaximumUrlLength) throw new InvalidOperationException($"URL exceeds limit: {url.Length}.");
            var decoded = Uri.UnescapeDataString(url);
            foreach (var expected in new[] { "Title & ? # + =", "Line one", "Line two — اردو", "[SOFT2027 Bug Report]" })
                if (!decoded.Contains(expected, StringComparison.Ordinal)) throw new InvalidOperationException($"Decoded URL did not preserve '{expected}'.");
            if (route == BugReportRoute.WhatsApp && !url.StartsWith("https://wa.me/15551234567?", StringComparison.Ordinal))
                throw new InvalidOperationException("WhatsApp number was not placed in the URL path.");
            Console.WriteLine($"PASS {route} URL encoding ({description})");
        }
        catch (Exception ex) { ReportFailure($"{route} URL encoding ({description})", ex); }
    }

    private static void Check(string name, Func<FrameworkElement> create)
    {
        Window? host = null;
        try
        {
            var element = create();
            if (element is Window window)
            {
                host = window;
                host.Width = 1600;
                host.Height = 950;
                host.ShowInTaskbar = false;
                host.WindowStyle = WindowStyle.None;
                host.Opacity = 0;
            }
            else
            {
                host = new Window { Content = element, Width = 1600, Height = 950, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Opacity = 0 };
            }

            host.Show();
            host.UpdateLayout();
            PumpLayout(host);
            CheckDataGridHeaders(host, name);
            Console.WriteLine($"PASS rendered {name}");
        }
        catch (Exception ex) { ReportFailure($"Rendered {name}", ex); }
        finally { try { host?.Close(); } catch { } }
    }

    private static void CheckWindowControlStates()
    {
        Window? host = null;
        try
        {
            host = new MainWindow { ShowInTaskbar = false, WindowStyle = WindowStyle.None, Opacity = 0 };
            host.Show();
            host.UpdateLayout();
            PumpLayout(host);

            var maximizeButton = VisualAndLogicalDescendants(host).OfType<Button>()
                .FirstOrDefault(button => AutomationProperties.GetName(button) == "Maximize or restore window")
                ?? throw new InvalidOperationException("The maximize/restore button was not found in MainWindow.");
            maximizeButton.ApplyTemplate();
            var maximizePath = maximizeButton.Template.FindName("MaximizeIcon", maximizeButton) as System.Windows.Shapes.Path
                ?? throw new InvalidOperationException("The normal-state maximize icon was not found in the button template.");
            var restorePath = maximizeButton.Template.FindName("RestoreIcon", maximizeButton) as System.Windows.Shapes.Path
                ?? throw new InvalidOperationException("The restore icon was not found in the button template.");
            var maximizeGeometry = maximizePath.Data?.ToString();
            if (string.IsNullOrWhiteSpace(maximizeGeometry) || maximizePath.Data is null || maximizePath.Data.Bounds.IsEmpty || maximizePath.Data.Bounds.Width <= 0 || maximizePath.Data.Bounds.Height <= 0 || maximizePath.Visibility != Visibility.Visible)
                throw new InvalidOperationException("The normal-state maximize icon has no rendered geometry or bounds.");

            host.WindowState = WindowState.Maximized;
            host.UpdateLayout();
            PumpLayout(host);
            if (restorePath.Data?.ToString() == maximizeGeometry || restorePath.Data is null || restorePath.Data.Bounds.IsEmpty || restorePath.Data.Bounds.Width <= 0 || restorePath.Data.Bounds.Height <= 0 || restorePath.Visibility != Visibility.Visible || maximizePath.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("The restore icon did not switch to a distinct, visible geometry.");
            if (restorePath.Stroke is not SolidColorBrush stroke || stroke.Color.A == 0)
                throw new InvalidOperationException("The restore icon has no visible stroke.");

            Console.WriteLine("PASS maximize/restore button renders distinct visible vector icons in both window states");
        }
        catch (Exception ex) { ReportFailure("Maximize/restore button states", ex); }
        finally { try { host?.Close(); } catch { } }
    }

    private static void CheckDataGridHeaders(DependencyObject root, string viewName)
    {
        var gridIndex = 0;
        foreach (var grid in VisualAndLogicalDescendants(root).OfType<DataGrid>().Distinct())
        {
            gridIndex++;
            for (DependencyObject? ancestor = grid; ancestor is not null; ancestor = LogicalTreeHelper.GetParent(ancestor) ?? (ancestor is Visual ? VisualTreeHelper.GetParent(ancestor) : null))
                if (ancestor is TabItem tab) tab.IsSelected = true;
            grid.Width = Math.Max(1450, double.IsNaN(grid.Width) ? 0 : grid.Width);
            grid.ItemsSource = new[] { DummyRow(viewName, gridIndex) };
            if (grid.Columns.Count == 0 && !grid.AutoGenerateColumns)
                grid.Columns.Add(new DataGridTextColumn { Header = "QA smoke header", Binding = new System.Windows.Data.Binding(nameof(SmokeRow.Value)) });

            grid.ApplyTemplate();
            Window.GetWindow(grid)?.UpdateLayout();
            grid.UpdateLayout();
            foreach (var column in grid.Columns)
            {
                var header = VisualDescendants(grid).OfType<DataGridColumnHeader>().FirstOrDefault(h => ReferenceEquals(h.Column, column));
                if (header is null)
                {
                    ReportFailure($"{viewName} DataGrid header '{column.Header}'", new InvalidOperationException("The header did not materialize in the rendered visual tree."));
                    continue;
                }
                var text = VisualDescendants(header).OfType<TextBlock>().FirstOrDefault(tb => !string.IsNullOrWhiteSpace(tb.Text));
                if (text is null)
                {
                    ReportFailure($"{viewName} DataGrid header '{column.Header}'", new InvalidOperationException("No rendered TextBlock was found inside the column header."));
                    continue;
                }
                var foreground = text.Foreground;
                var background = header.Background;
                if (foreground is not SolidColorBrush fg || background is not SolidColorBrush bg)
                {
                    ReportFailure($"{viewName} DataGrid header '{column.Header}'", new InvalidOperationException("Effective TextBlock foreground and header background must be solid brushes."));
                    continue;
                }
                var ratio = ContrastRatio(fg.Color, bg.Color);
                DataGridHeaderCount++;
                ContrastRows.Add(($"DataGrid header · {viewName} · {text.Text}", Hex(fg.Color), Hex(bg.Color), ratio));
                if (ratio < 4.5)
                    ReportFailure($"{viewName} DataGrid header '{text.Text}' contrast", new InvalidOperationException($"Rendered TextBlock foreground {Hex(fg.Color)} on {Hex(bg.Color)} is {ratio:F2}:1; required >= 4.5:1."));
                var shared = (Style?)Application.Current.TryFindResource("SharedDataGridColumnHeaderStyle");
                if (shared is null || !DerivesFrom(column.HeaderStyle ?? grid.ColumnHeaderStyle, shared))
                    ReportFailure($"{viewName} DataGrid header style", new InvalidOperationException($"Column '{column.Header}' does not use SharedDataGridColumnHeaderStyle."));
            }
        }
    }

    private static object DummyRow(string viewName, int gridIndex) => viewName switch
    {
        "Products, Customers, Suppliers" => new Customer { CustomerCode = "QA-CUST-001", CustomerName = "QA Customer" },
        "Categories, Brands, Units" => new LookupItem { Id = -1, Name = "QA Lookup" },
        "Customer and Supplier Ledgers" => new CustomerLedgerEntry { TransactionType = "QA", ReferenceNumber = "QA-LEDGER-001", Description = "QA ledger row" },
        "Administration and Activity" when gridIndex == 1 => new AdminUserSummary { Username = "qa.user", FullName = "QA User", Role = "Employee" },
        "Administration and Activity" => new AuditLogEntry { AdminName = "QA User", Action = "QA action", ModuleName = "QA module", RecordReference = "QA-001", PreviousValue = "QA", NewValue = "QA" },
        _ => new SmokeRow()
    };

    private static bool DerivesFrom(Style? candidate, Style required)
    {
        for (var style = candidate; style is not null; style = style.BasedOn)
            if (ReferenceEquals(style, required)) return true;
        return false;
    }

    private static void CheckRenderedStateProbes()
    {
        Window? host = null;
        try
        {
            var root = new StackPanel { Background = Brushes.White, Width = 1300 };
            var selectedGrid = new DataGrid { Height = 100, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single, ItemsSource = new ObservableCollection<SmokeRow> { new() { Value = "Selected QA row" } } };
            selectedGrid.Columns.Add(new DataGridTextColumn { Header = "Selected row", Binding = new System.Windows.Data.Binding(nameof(SmokeRow.Value)) });
            root.Children.Add(selectedGrid);
            var disabled = new Button { Content = "Disabled QA button", IsEnabled = false, Width = 260, Height = 44 };
            root.Children.Add(disabled);
            var tabs = new TabControl { Width = 500, Height = 90, Items = { new TabItem { Header = "QA tab", Content = "Tab content" } } };
            root.Children.Add(tabs);
            var sidebar = new Button { Content = "QA sidebar selected", Style = (Style?)Application.Current.TryFindResource("SidebarNavItemStyle"), Tag = true, Width = 280, Height = 44 };
            root.Children.Add(sidebar);
            var placeholder = new TextBox { Tag = "QA placeholder text", Width = 280, Height = 44 };
            root.Children.Add(placeholder);
            foreach (var message in new[] { "Error: QA failure", "Saved QA changes", "QA information" })
            {
                var converter = new StatusMessageConverter();
                var status = new TextBlock
                {
                    Text = (string)converter.Convert(message, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture)!,
                    Foreground = (Brush)converter.Convert(message, typeof(Brush), "Color", System.Globalization.CultureInfo.InvariantCulture)!,
                    Background = Brushes.White,
                    Margin = new Thickness(2)
                };
                root.Children.Add(status);
            }
            var tip = new ToolTip { Content = "QA tooltip with readable text" };
            ToolTipService.SetToolTip(disabled, tip);
            tip.IsOpen = true;

            host = new Window { Content = root, Width = 1400, Height = 700, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Opacity = 0 };
            host.Show();
            host.UpdateLayout();
            PumpLayout(host);
            if (selectedGrid.Items.Count > 0) selectedGrid.SelectedIndex = 0;
            selectedGrid.UpdateLayout();
            PumpLayout(host);

            var selectedRow = VisualDescendants(selectedGrid).OfType<DataGridRow>().FirstOrDefault();
            CheckText("Selected row", VisualDescendants(selectedGrid).OfType<TextBlock>().FirstOrDefault(tb => tb.Text == "Selected QA row"), selectedRow?.Background);
            CheckText("Disabled button", VisualDescendants(disabled).OfType<TextBlock>().FirstOrDefault(tb => tb.Text.Contains("Disabled QA button")), disabled.Background);
            CheckText("Tab header", VisualDescendants(tabs).OfType<TextBlock>().FirstOrDefault(tb => tb.Text == "QA tab"), tabs.Background);
            CheckText("Selected sidebar item", VisualDescendants(sidebar).OfType<TextBlock>().FirstOrDefault(tb => tb.Text.Contains("QA sidebar")), (Brush?)Application.Current.TryFindResource("Navy2Brush"));
            CheckText("Placeholder", VisualDescendants(placeholder).OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "Placeholder"), placeholder.Background);
            CheckText("Tooltip", VisualDescendants(tip).OfType<TextBlock>().FirstOrDefault(tb => tb.Text.Contains("QA tooltip")), tip.Background);
            foreach (var status in VisualDescendants(root).OfType<TextBlock>().Where(tb => tb.Text.StartsWith("⚠ ") || tb.Text.StartsWith("✓ ") || tb.Text.StartsWith("ℹ ")))
                CheckText($"Status · {status.Text[..2].Trim()}", status, Brushes.White);
        }
        catch (Exception ex) { ReportFailure("Rendered state probes", ex); }
        finally { try { host?.Close(); } catch { } }
    }

    private static void CheckText(string name, TextBlock? text, Brush? background)
    {
        if (text is null) { ReportFailure(name, new InvalidOperationException("Rendered text element was not found.")); return; }
        if (text.Foreground is not SolidColorBrush fg || background is not SolidColorBrush bg)
        { ReportFailure(name, new InvalidOperationException("Effective foreground/background were not solid brushes.")); return; }
        var ratio = ContrastRatio(fg.Color, bg.Color);
        ContrastRows.Add((name, Hex(fg.Color), Hex(bg.Color), ratio));
        if (ratio < 4.5) ReportFailure(name, new InvalidOperationException($"Rendered foreground/background contrast is {ratio:F2}:1; required >= 4.5:1."));
    }

    private static void PumpLayout(Window? window)
    {
        window?.UpdateLayout();
    }


    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        yield return root;
        if (root is not Visual && root is not System.Windows.Media.Media3D.Visual3D) yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisualDescendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private static IEnumerable<DependencyObject> VisualAndLogicalDescendants(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); if (!seen.Add(current)) continue;
            yield return current;
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) pending.Push(VisualTreeHelper.GetChild(current, i));
            foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>()) pending.Push(child);
        }
    }

    private static double ContrastRatio(Color fg, Color bg)
    {
        static double L(Color c)
        {
            static double Ch(byte b) { var v = b / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Ch(c.R) + .7152 * Ch(c.G) + .0722 * Ch(c.B);
        }
        var a = L(fg); var b = L(bg); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static void CheckColorResourceTypes()
    {
        var uiRoot = Path.Combine(FindProjectRoot(), "src", "SOFT2027.UI");
        if (!Directory.Exists(uiRoot)) { ReportFailure("XAML Color resource type audit", new DirectoryNotFoundException($"UI XAML folder not found: {uiRoot}")); return; }
        var declaredTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var documents = Directory.GetFiles(uiRoot, "*.xaml", SearchOption.AllDirectories).Select(path => (Path: path, Document: XDocument.Load(path))).ToArray();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var (_, document) in documents)
            foreach (var element in document.Descendants())
                if (element.Attribute(x + "Key") is { } key && element.Name.LocalName is "Color" or "SolidColorBrush" or "LinearGradientBrush" or "RadialGradientBrush") declaredTypes[key.Value] = element.Name.LocalName;
        var issues = new List<string>();
        var resourceReference = new Regex(@"\{(?:StaticResource|DynamicResource)\s+([^}]+)\}", RegexOptions.Compiled);
        foreach (var (path, document) in documents)
            foreach (var element in document.Descendants())
                foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName == "Color"))
                    foreach (Match match in resourceReference.Matches(attribute.Value))
                    {
                        var key = match.Groups[1].Value.Trim();
                        if (declaredTypes.TryGetValue(key, out var type) && type != "Color") issues.Add($"{Path.GetFileName(path)}: Color references {type} resource '{key}'.");
                    }
        if (issues.Count > 0) ReportFailure("XAML Color resource type audit", new InvalidOperationException(string.Join(Environment.NewLine, issues)));
        else Console.WriteLine("PASS XAML Color resource type audit");
    }

    private static void ReportFailure(string name, Exception exception)
    {
        Failures.Add(name); Console.Error.WriteLine($"FAIL {name}: {exception}");
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException) Console.Error.WriteLine($"  INNER: {inner.GetType().FullName}: {inner.Message}");
    }

    private sealed class SmokeRow { public string Value { get; set; } = "QA dummy row"; public string Name { get; set; } = "QA dummy row"; }
}
