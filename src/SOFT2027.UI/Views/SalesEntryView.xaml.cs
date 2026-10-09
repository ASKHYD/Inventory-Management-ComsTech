using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views
{
    /// <summary>
    /// Interaction logic for SalesEntryView.xaml
    /// </summary>
    public partial class SalesEntryView : UserControl
    {
        public SalesEntryView()
        {
            InitializeComponent();
        }

        private void View_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SalesEntryViewModel viewModel && viewModel.LoadInitialDataCommand.CanExecute(null))
                viewModel.LoadInitialDataCommand.Execute(null);
        }

        private void EnterNewPrice_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UnitPriceInput.Focus();
            UnitPriceInput.SelectAll();
        }

        private void UIElement_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = System.Windows.UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((System.Windows.Controls.Control)sender).Parent as System.Windows.UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
