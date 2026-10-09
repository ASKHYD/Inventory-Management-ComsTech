using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IAppContainer? Container { get; set; }

    public static string GetDatabasePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "SOFT2027");
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }
        return Path.Combine(appDir, "soft2027.db");
    }

    private static string TourCompletionPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOFT2027", "getting-started.completed");

    public static bool HasCompletedTour() => File.Exists(TourCompletionPath);

    public static void MarkTourCompleted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TourCompletionPath)!);
        File.WriteAllText(TourCompletionPath, DateTimeOffset.UtcNow.ToString("O"));
    }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            string dbPath;
            try
            {
                dbPath = GetDatabasePath();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize application storage directory.\nError: {ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            var connectionString = $"Data Source={dbPath};Foreign Keys=True;";

            try
            {
                Container = Bootstrapper.CreateContainer(connectionString);

                var initializer = Container.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database initialization failed.\nError: {ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            var mainVm = Container.GetRequiredService<MainViewModel>();
            try
            {
                await mainVm.InitializeSecurityAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not prepare administrator sign-in. {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOFT2027", "errors.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"{DateTimeOffset.Now:O}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { /* Preserve the original error even if local logging is unavailable. */ }

            MessageBox.Show(
                $"The app hit an unexpected problem. It has been recorded here:\n{logPath}\n\n{e.Exception.Message}",
                "SOFT2027 error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
