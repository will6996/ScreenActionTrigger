using System.Globalization;
using System.Windows.Data;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(bool), typeof(string))]
public sealed class PauseResumeConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? "▶  Continuar" : "⏸  Pausar";
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
