using System.Globalization;
using System.Windows.Data;

namespace ScreenActionTrigger.UI.Converters;

public sealed class PanelChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "◀" : "▶";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
