using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            Loaded += LoginView_Loaded;
            DataContextChanged += LoginView_DataContextChanged;
            LoginPasswordBox.PasswordChanged += LoginPasswordBox_PasswordChanged;
            ConfirmPasswordBox.PasswordChanged += ConfirmPasswordBox_PasswordChanged;
        }

        private void LoginView_Loaded(object sender, RoutedEventArgs e)
        {
            SyncPasswordFromViewModel();
        }

        private void LoginView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldViewModel) oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            if (e.NewValue is MainViewModel newViewModel) newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            SyncPasswordFromViewModel();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.LoginPassword) or nameof(MainViewModel.ConfirmPassword))
                SyncPasswordFromViewModel();
        }

        private void SyncPasswordFromViewModel()
        {
            if (DataContext is not MainViewModel vm) return;
            if (string.IsNullOrEmpty(vm.LoginPassword)) LoginPasswordBox.Clear();
            else if (string.IsNullOrEmpty(LoginPasswordBox.Password)) LoginPasswordBox.Password = vm.LoginPassword;
            if (string.IsNullOrEmpty(vm.ConfirmPassword)) ConfirmPasswordBox.Clear();
            else if (string.IsNullOrEmpty(ConfirmPasswordBox.Password)) ConfirmPasswordBox.Password = vm.ConfirmPassword;
        }

        private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.LoginPassword = LoginPasswordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm) vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (LoginPasswordBox.Visibility == Visibility.Visible)
            {
                LoginPasswordTextBox.Text = LoginPasswordBox.Password;
                LoginPasswordBox.Visibility = Visibility.Collapsed;
                LoginPasswordTextBox.Visibility = Visibility.Visible;
                PasswordVisibilityToggle.Content = "Hide";
                LoginPasswordTextBox.Focus();
                LoginPasswordTextBox.CaretIndex = LoginPasswordTextBox.Text.Length;
            }
            else
            {
                LoginPasswordBox.Password = LoginPasswordTextBox.Text;
                LoginPasswordTextBox.Visibility = Visibility.Collapsed;
                LoginPasswordBox.Visibility = Visibility.Visible;
                PasswordVisibilityToggle.Content = "Show";
                LoginPasswordBox.Focus();
            }
        }
    }
}
