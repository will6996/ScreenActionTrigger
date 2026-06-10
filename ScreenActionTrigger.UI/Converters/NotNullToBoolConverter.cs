using System.Globalization;
using System.Windows.Data;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c) => value is not null;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
