using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(double), typeof(SolidColorBrush))]
public sealed class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is double conf)
        {
            if (conf >= 0.9)  return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
            if (conf >= 0.7)  return new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            return                   new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
