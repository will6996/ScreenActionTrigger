using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

[ValueConversion(typeof(ActionType), typeof(Visibility))]
public sealed class ActionTypeToPathVisConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is ActionType.MouseFollowPath ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
