using System.Globalization;
using Microsoft.Maui.Controls;

namespace SuleymaniyeCalendar.Converters
{
    public class BoolToBellIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "\uf0f3" : "\uf1f6";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
