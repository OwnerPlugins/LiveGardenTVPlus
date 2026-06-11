#nullable disable
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LiveGardenTVPlus.Converters
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string url = value as string;
            if (string.IsNullOrEmpty(url))
                return null;
            try
            {
                return new BitmapImage(new Uri(url, UriKind.Absolute));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
