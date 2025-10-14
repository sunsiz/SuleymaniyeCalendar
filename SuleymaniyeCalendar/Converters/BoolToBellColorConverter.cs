using System.Globalization;
using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Converters
{
    public class BoolToBellColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var colorKey = (bool)value ? "Success" : "OnSurfaceVariantLight";
            if (Application.Current.Resources.TryGetValue(colorKey, out var colorResource))
            {
                return (Color)colorResource;
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
