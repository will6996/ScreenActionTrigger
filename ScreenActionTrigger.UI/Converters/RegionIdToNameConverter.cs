using System.Globalization;
using System.Windows.Data;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.Converters;

public sealed class RegionIdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Guid id) return string.Empty;
        if (values[1] is not IEnumerable<MonitoredRegion> regions) return id.ToString()[..8];

        return regions.FirstOrDefault(r => r.Id == id)?.Name ?? $"Slot {id.ToString()[..8]}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
