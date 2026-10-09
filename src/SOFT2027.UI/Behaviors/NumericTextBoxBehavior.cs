using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SOFT2027.UI.Behaviors;

/// <summary>Provides numeric-only entry and keyboard increment/decrement for numeric fields.</summary>
public static class NumericTextBoxBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(false));

    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached(
        "Step", typeof(decimal), typeof(NumericTextBoxBehavior), new PropertyMetadata(1m));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);
    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static decimal GetStep(DependencyObject element) => (decimal)element.GetValue(StepProperty);
    public static void SetStep(DependencyObject element, decimal value) => element.SetValue(StepProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;
        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var candidate = ReplaceSelection(textBox, e.Text);
        if (!IsValidPartialNumber(candidate, GetAllowNegative(textBox))) e.Handled = true;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key is not (Key.Up or Key.Down)) return;

        var current = decimal.TryParse(textBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ? value : 0m;
        var next = current + (e.Key == Key.Up ? GetStep(textBox) : -GetStep(textBox));
        if (!GetAllowNegative(textBox)) next = Math.Max(0m, next);
        textBox.SetCurrentValue(TextBox.TextProperty, next.ToString("0.############################", CultureInfo.CurrentCulture));
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.SourceDataObject.GetDataPresent(DataFormats.Text, true))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!IsValidPartialNumber(ReplaceSelection(textBox, pasted), GetAllowNegative(textBox))) e.CancelCommand();
    }

    private static string ReplaceSelection(TextBox textBox, string insertion) =>
        textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, insertion);

    private static bool IsValidPartialNumber(string value, bool allowNegative)
    {
        if (value.Length == 0) return true;
        if (allowNegative && value == "-") return true;

        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (value == separator || (allowNegative && value == "-" + separator)) return true;
        if (value.Count(char.IsDigit) != value.Length - (allowNegative && value.StartsWith("-", StringComparison.Ordinal) ? 1 : 0)
            - (value.Contains(separator, StringComparison.Ordinal) ? separator.Length : 0)) return false;
        if (value.Contains(separator, StringComparison.Ordinal) && value.IndexOf(separator, StringComparison.Ordinal) != value.LastIndexOf(separator, StringComparison.Ordinal)) return false;
        if (!allowNegative && value.StartsWith("-", StringComparison.Ordinal)) return false;
        if (allowNegative && value.Count(c => c == '-') > 1) return false;
        return !value.Contains('-') || value[0] == '-';
    }
}
