using System.Windows.Controls;

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
