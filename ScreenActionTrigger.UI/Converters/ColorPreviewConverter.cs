using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public sealed class ColorPreviewConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        try
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
        }
        catch { }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
    {
        if (value is SolidColorBrush b)
            return b.Color.ToString();
        return "#000000";
    }
}
