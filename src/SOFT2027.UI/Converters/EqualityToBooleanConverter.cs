using System;
using System.Globalization;
using System.Windows.Data;

namespace SOFT2027.UI.Converters;

/// <summary>
/// Compares a bound string or object with a ConverterParameter.
/// Used for Quick Filter Chips (e.g. All, Active, Inactive).
/// </summary>
public sealed class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}
