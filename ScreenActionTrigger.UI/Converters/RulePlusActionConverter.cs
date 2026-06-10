using System.Globalization;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

/// <summary>
/// Packs (VisualRule, TriggerAction) into a tuple for RemoveActionFromRuleCommand.
/// </summary>
public sealed class RulePlusActionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        var rule   = values.Length > 0 ? values[0] as VisualRule   : null;
        var action = values.Length > 1 ? values[1] as TriggerAction : null;
        return (rule, action);
    }

    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
