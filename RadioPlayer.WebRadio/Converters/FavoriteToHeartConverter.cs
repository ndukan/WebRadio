
using System.Windows;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace RadioPlayer.WebRadio.Converters
{
    public class FavoriteToHeartConverter : IValueConverter
    {
        public static FavoriteToHeartConverter Instance { get; } = new FavoriteToHeartConverter();


        public Brush FavoriteColor { get; set; } = Brushes.Red;
        public Brush NonFavoriteColor { get; set; } = Brushes.White;



        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isFavorite = value as bool? ?? false;
            return isFavorite ? "♥" : "♡";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
