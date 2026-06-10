using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

/// <summary>Shows the key-name TextBox only for keyboard action types.</summary>
[ValueConversion(typeof(ActionType), typeof(Visibility))]
public sealed class ActionTypeToKeyVisConverter : IValueConverter
{
    private static readonly HashSet<ActionType> _keyboardTypes = new()
    {
        ActionType.KeyPress,
        ActionType.KeyHold,
        ActionType.KeyRelease,
        ActionType.KeyCombination
    };

    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is ActionType at && _keyboardTypes.Contains(at)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
