using System;
using System.Globalization;
using System.Windows.Data;

namespace SOFT2027.UI.Converters
{
    /// <summary>
    /// Calculates a viewport proportional value (e.g., 0.8 for 80vh of window height)
    /// to ensure modals remain fully inside visible boundaries on 1366x768 screens.
    /// </summary>
    public class ViewportRatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double dimension = 600.0;
            if (value is double d && !double.IsNaN(d) && !double.IsInfinity(d) && d > 100)
            {
                dimension = d;
            }

            double ratio = 0.80; // default 80% (80vh)
            if (parameter != null)
            {
                if (parameter is double pDouble)
                {
                    ratio = pDouble;
                }
                else if (double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    ratio = parsed;
                }
            }

            return Math.Max(300.0, dimension * ratio);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
