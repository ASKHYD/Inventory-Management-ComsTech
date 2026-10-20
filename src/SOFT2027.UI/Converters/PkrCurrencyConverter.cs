using System;
using System.Globalization;
using System.Windows.Data;
using SOFT2027.UI.Behaviors;

namespace SOFT2027.UI.Converters;

/// <summary>
/// Converts decimal currency values to Pakistani Rupee formatted strings ("PKR 50,000.00" or "50,000.00")
/// and parses operator text inputs with commas or currency symbols back to decimal with zero data loss.
/// </summary>
public sealed class PkrCurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;

        decimal amount;
        if (value is decimal d) amount = d;
        else if (value is double db) amount = (decimal)db;
        else if (value is float f) amount = (decimal)f;
        else if (value is int i) amount = i;
        else if (value is long l) amount = l;
        else if (!decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            return value.ToString();

        var withPrefix = string.Equals(parameter as string, "WithPrefix", StringComparison.OrdinalIgnoreCase);
        if (withPrefix)
        {
            return $"PKR {amount.ToString("N2", CultureInfo.InvariantCulture)}";
        }

        return amount % 1 == 0m
            ? amount.ToString("N0", CultureInfo.InvariantCulture)
            : amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return 0m;

        var text = value.ToString();
        var cleaned = NumericTextBoxBehavior.CleanNumericString(text);
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ||
            decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out result))
        {
            if (targetType == typeof(decimal?)) return (decimal?)result;
            if (targetType == typeof(decimal)) return result;
            if (targetType == typeof(int)) return (int)result;
            if (targetType == typeof(int?)) return (int?)result;
            if (targetType == typeof(long)) return (long)result;
            if (targetType == typeof(long?)) return (long?)result;
            if (targetType == typeof(double)) return (double)result;
            if (targetType == typeof(double?)) return (double?)result;
            return result;
        }

        return targetType == typeof(decimal?) ? null : 0m;
    }
}
