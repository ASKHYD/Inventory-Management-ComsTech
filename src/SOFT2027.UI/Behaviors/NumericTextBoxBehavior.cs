using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SOFT2027.UI.Behaviors;

/// <summary>
/// Provides numeric-only entry, Pakistani Rupee (PKR) digit grouping (commas),
/// auto-SelectAll on focus for 1-key overwrite, and keyboard increment/decrement.
/// </summary>
public static class NumericTextBoxBehavior
{
    static NumericTextBoxBehavior()
    {
        // Global Auto-Select on Focus across all TextBoxes in the app:
        // When tabbing or clicking into any numeric or text field, the entire existing content
        // is automatically selected so typing a new number replaces the placeholder immediately.
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnAnyTextBoxGotKeyboardFocus));

        EventManager.RegisterClassHandler(
            typeof(TextBox),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnAnyTextBoxPreviewMouseLeftButtonDown),
            true);
    }

    private static void OnAnyTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
        }
    }

    private static void OnAnyTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            e.Handled = true;
        }
    }

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(false));

    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached(
        "Step", typeof(decimal), typeof(NumericTextBoxBehavior), new PropertyMetadata(1m));

    public static readonly DependencyProperty FormatOnLostFocusProperty = DependencyProperty.RegisterAttached(
        "FormatOnLostFocus", typeof(bool), typeof(NumericTextBoxBehavior), new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);
    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static decimal GetStep(DependencyObject element) => (decimal)element.GetValue(StepProperty);
    public static void SetStep(DependencyObject element, decimal value) => element.SetValue(StepProperty, value);
    public static bool GetFormatOnLostFocus(DependencyObject element) => (bool)element.GetValue(FormatOnLostFocusProperty);
    public static void SetFormatOnLostFocus(DependencyObject element, bool value) => element.SetValue(FormatOnLostFocusProperty, value);

    [ThreadStatic]
    private static bool _isFormatting;

    private static readonly Converters.PkrCurrencyConverter _sharedConverter = new();

    private static void EnsureBindingConverter(TextBox textBox)
    {
        var bindingExpr = System.Windows.Data.BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
        if (bindingExpr?.ParentBinding != null && bindingExpr.ParentBinding.Converter == null)
        {
            var oldBinding = bindingExpr.ParentBinding;
            var newBinding = new System.Windows.Data.Binding
            {
                Path = oldBinding.Path,
                Mode = oldBinding.Mode,
                UpdateSourceTrigger = oldBinding.UpdateSourceTrigger,
                Converter = _sharedConverter,
                ConverterParameter = oldBinding.ConverterParameter,
                ValidatesOnDataErrors = oldBinding.ValidatesOnDataErrors,
                ValidatesOnExceptions = oldBinding.ValidatesOnExceptions,
                NotifyOnValidationError = oldBinding.NotifyOnValidationError,
                StringFormat = oldBinding.StringFormat
            };

            if (oldBinding.Source != null)
            {
                newBinding.Source = oldBinding.Source;
            }
            else if (!string.IsNullOrEmpty(oldBinding.ElementName))
            {
                newBinding.ElementName = oldBinding.ElementName;
            }
            else if (oldBinding.RelativeSource != null)
            {
                newBinding.RelativeSource = oldBinding.RelativeSource;
            }

            System.Windows.Data.BindingOperations.SetBinding(textBox, TextBox.TextProperty, newBinding);
        }
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;
        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.TextChanged += OnTextChanged;
            textBox.LostFocus += OnLostFocus;
            textBox.Loaded += OnTextBoxLoaded;
            DataObject.AddPastingHandler(textBox, OnPasting);
            EnsureBindingConverter(textBox);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.TextChanged -= OnTextChanged;
            textBox.LostFocus -= OnLostFocus;
            textBox.Loaded -= OnTextBoxLoaded;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox) EnsureBindingConverter(textBox);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var candidate = ReplaceSelection(textBox, e.Text);
        if (!IsValidPartialNumber(candidate, GetAllowNegative(textBox))) e.Handled = true;
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFormatting || sender is not TextBox textBox || !GetIsEnabled(textBox)) return;

        EnsureBindingConverter(textBox);

        var text = textBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var isNegative = text.StartsWith('-');
        var core = isNegative ? text.Substring(1) : text;

        var dotIndex = core.IndexOf('.');
        string intPart = dotIndex >= 0 ? core.Substring(0, dotIndex) : core;
        string decPart = dotIndex >= 0 ? core.Substring(dotIndex) : "";

        var rawInt = intPart.Replace(",", "");
        if (rawInt.Length == 0 && decPart.Length == 0) return;
        if (!rawInt.All(char.IsDigit)) return;

        if (rawInt.Length > 0 && decimal.TryParse(rawInt, NumberStyles.None, CultureInfo.InvariantCulture, out var num))
        {
            var formattedInt = num.ToString("N0", CultureInfo.InvariantCulture);
            var formatted = (isNegative ? "-" : "") + formattedInt + decPart;

            if (formatted != text)
            {
                var caret = textBox.SelectionStart;
                var rawBeforeCaret = text.Take(caret).Count(c => c != ',');

                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (textBox.Text != formatted)
                    {
                        _isFormatting = true;
                        try
                        {
                            textBox.Text = formatted;
                            int newCaret = 0;
                            int seenRaw = 0;
                            while (newCaret < formatted.Length && seenRaw < rawBeforeCaret)
                            {
                                if (formatted[newCaret] != ',') seenRaw++;
                                newCaret++;
                            }
                            textBox.SelectionStart = Math.Min(newCaret, formatted.Length);
                            textBox.SelectionLength = 0;
                        }
                        finally
                        {
                            _isFormatting = false;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || !GetFormatOnLostFocus(textBox)) return;
        if (string.IsNullOrWhiteSpace(textBox.Text)) return;

        var cleaned = CleanNumericString(textBox.Text);
        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
            decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value))
        {
            if (!GetAllowNegative(textBox) && value < 0m) value = 0m;
            var formatted = (value % 1 == 0m && !textBox.Text.Contains('.'))
                ? value.ToString("N0", CultureInfo.InvariantCulture)
                : value.ToString("N2", CultureInfo.InvariantCulture);

            _isFormatting = true;
            textBox.Text = formatted;
            _isFormatting = false;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || Keyboard.Modifiers != ModifierKeys.None) return;

        // Smart Backspace: when caret is right after a comma, delete the preceding digit as well
        if (e.Key == Key.Back && textBox.SelectionLength == 0 && textBox.SelectionStart > 1)
        {
            if (textBox.Text[textBox.SelectionStart - 1] == ',')
            {
                var pos = textBox.SelectionStart;
                var text = textBox.Text.Remove(pos - 2, 2);
                _isFormatting = true;
                textBox.Text = text;
                _isFormatting = false;
                textBox.SelectionStart = Math.Max(0, pos - 2);
                e.Handled = true;
                return;
            }
        }

        if (e.Key is not (Key.Up or Key.Down)) return;

        var cleaned = CleanNumericString(textBox.Text);
        var current = decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        var next = current + (e.Key == Key.Up ? GetStep(textBox) : -GetStep(textBox));
        if (!GetAllowNegative(textBox)) next = Math.Max(0m, next);
        _isFormatting = true;
        textBox.Text = (next % 1 == 0m)
            ? next.ToString("N0", CultureInfo.InvariantCulture)
            : next.ToString("N2", CultureInfo.InvariantCulture);
        _isFormatting = false;
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
        var candidate = ReplaceSelection(textBox, pasted);
        if (!IsValidPartialNumber(candidate, GetAllowNegative(textBox))) e.CancelCommand();
    }

    private static string ReplaceSelection(TextBox textBox, string insertion) =>
        textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, insertion);

    public static string CleanNumericString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Replace("PKR", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Rs.", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Rs", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(",", "")
                    .Trim();
    }

    private static bool IsValidPartialNumber(string value, bool allowNegative)
    {
        if (string.IsNullOrEmpty(value)) return true;
        var cleaned = CleanNumericString(value);
        if (cleaned.Length == 0) return true;
        if (allowNegative && cleaned == "-") return true;

        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (cleaned == separator || (allowNegative && cleaned == "-" + separator)) return true;
        if (separator != "." && (cleaned == "." || (allowNegative && cleaned == "-."))) return true;

        int sepCount = 0;
        int minusCount = 0;

        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (char.IsDigit(c))
            {
                continue;
            }
            if (c == '.' || (separator.Length == 1 && c == separator[0]))
            {
                sepCount++;
                if (sepCount > 1) return false;
            }
            else if (c == '-' && i == 0 && allowNegative)
            {
                minusCount++;
                if (minusCount > 1) return false;
            }
            else
            {
                return false; // Rejects alphabetic characters, spaces, and punctuation
            }
        }

        return true;
    }
}
