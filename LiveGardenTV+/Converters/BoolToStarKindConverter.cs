#nullable disable
using MaterialDesignThemes.Wpf;
using System.Globalization;
using System.Windows.Data;

namespace LiveGardenTVPlus.Converters
{
    public class BoolToStarKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isFavorite = (value is bool b && b);
            return isFavorite ? PackIconKind.Star : PackIconKind.StarOutline;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
