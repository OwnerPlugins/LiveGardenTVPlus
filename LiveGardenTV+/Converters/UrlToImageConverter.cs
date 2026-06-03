using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace LiveGardenTVPlus.Converters
{
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                try {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(url, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnDemand;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Failed to load image: {url} - {ex.Message}");
                    return null;
                }
            }
            return null;
        }
            
/*             if (value is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(url, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnDemand;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch { return null; }
            }
            return null;
        } */

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}