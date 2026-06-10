using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace ScreenActionTrigger.UI.Converters;

public sealed class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is Enum e)
        {
            var fi   = e.GetType().GetField(e.ToString());
            var desc = fi?.GetCustomAttribute<DescriptionAttribute>();
            return desc?.Description ?? e.ToString();
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
