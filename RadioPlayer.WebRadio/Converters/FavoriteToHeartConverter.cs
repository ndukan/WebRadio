using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace RadioPlayer.WebRadio.Converters
{
    public class FavoriteToHeartConverter : IValueConverter
    {
        public static FavoriteToHeartConverter Instance { get; } = new FavoriteToHeartConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //return (value is bool isFavorite && isFavorite) ? "❤️" : "🤍";

            //bool isFavorite = (bool)value;
            //System.Diagnostics.Trace.WriteLine($"🎯 Converter called: {isFavorite}");
            //return isFavorite ? "❤️" : "🤍";

            if (value is bool isFavorite)
            {
                //var result = isFavorite ? "❤️" : "🤍";
                var result = isFavorite ? "♥" : "♡";

                Trace.WriteLine($"🎯 Converter: {isFavorite} -> {result}");
                return result;
            }
            return "♡";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
