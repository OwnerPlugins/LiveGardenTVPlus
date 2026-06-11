#nullable disable
using System.Globalization;
using System.Windows.Data;

namespace LiveGardenTVPlus.Converters
{
    public class FirstUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<string> urls && urls != null && urls.Count > 0)
                return urls.First();
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}