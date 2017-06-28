using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChewsiPlugin.UI.Converters
{
    internal class GreaterThanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return int.Parse(value.ToString()) > int.Parse(parameter.ToString()) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception)
            {
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}