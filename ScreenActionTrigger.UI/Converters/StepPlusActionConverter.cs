using System.Globalization;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

public sealed class StepPlusActionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        var step   = values.Length > 0 ? values[0] as SequenceStep : null;
        var action = values.Length > 1 ? values[1] as TriggerAction : null;
        return (step, action);
    }

    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
