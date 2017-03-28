using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ChewsiPlugin.UI.Converters
{
    internal class BooleanToVisibilityInverseConverter : IValueConverter
    {
        BooleanToVisibilityConverter converter = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return converter.ConvertBack(value, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return converter.Convert(value, targetType, parameter, culture);
        }
    }
}
