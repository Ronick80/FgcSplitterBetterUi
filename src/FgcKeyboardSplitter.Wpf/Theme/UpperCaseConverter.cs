using System.Globalization;
using System.Windows.Data;

namespace FgcKeyboardSplitter.Theme;

/// <summary>WPF has no text-transform, so arcade headings upper-case their bound text with this.</summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string)?.ToUpper(culture) ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
