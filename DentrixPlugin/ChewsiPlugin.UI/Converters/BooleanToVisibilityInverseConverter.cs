using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ChewsiPlugin.UI.Converters
{
    internal class BooleanToVisibilityInverseConverter : IValueConverter
    {
        readonly BooleanToVisibilityConverter _converter = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (bool) value;
            return _converter.Convert(!val, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
