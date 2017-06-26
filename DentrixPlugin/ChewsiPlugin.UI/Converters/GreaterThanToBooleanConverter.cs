using System;
using System.Globalization;
using System.Windows.Data;

namespace ChewsiPlugin.UI.Converters
{
    internal class GreaterThanToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return int.Parse(value.ToString()) > int.Parse(parameter.ToString());
            }
            catch (Exception)
            {
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}