#nullable disable
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LiveGardenTVPlus.Converters
{
    public class BoolToStarColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isFavorite = (value is bool b && b);
            return isFavorite ? new SolidColorBrush(Colors.Gold) : new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
