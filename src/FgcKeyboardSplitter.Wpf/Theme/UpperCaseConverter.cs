using System.Globalization;
using System.Windows.Data;

namespace FgcKeyboardSplitter.Theme;

/// <summary>WPF has no text-transform, so arcade headings upper-case their bound text with this.</summary>
public sealed class UpperCaseConverter : IValueConverter
{
    private const char MicroSign = 'µ';

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text ? ToUpper(text, culture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>
    /// Upper case, except a micro unit such as "µs": the micro sign would become the Greek capital Mu, which looks
    /// like an M and turns microseconds into what reads as "MS" (milliseconds). The unit letter after it keeps its
    /// case too, so the label shows "µs".
    /// </summary>
    public static string ToUpper(string text, CultureInfo culture)
    {
        var result = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            bool unit = text[i] == MicroSign || (i > 0 && text[i - 1] == MicroSign);
            result[i] = unit ? text[i] : char.ToUpper(text[i], culture);
        }

        return new string(result);
    }
}
