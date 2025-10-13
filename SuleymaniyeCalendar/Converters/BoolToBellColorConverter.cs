using System.Globalization;
using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Converters
{
    public class BoolToBellColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Application.Current.Resources.TryGetValue((bool)value ? "Success" : "OnSurfaceVariant", out var colorResource))
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
