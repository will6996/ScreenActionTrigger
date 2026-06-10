using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(ActionType), typeof(Visibility))]
public sealed class ActionTypeToMouseVisConverter : IValueConverter
{
    private static readonly HashSet<ActionType> _mouseTypes = new()
    {
        ActionType.MouseLeftClick,
        ActionType.MouseRightClick,
        ActionType.MouseDoubleClick,
        ActionType.MousePress,
        ActionType.MouseRelease,
        ActionType.MouseScroll
    };

    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is ActionType at && _mouseTypes.Contains(at)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
