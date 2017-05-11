using System;
using System.Globalization;
using System.Windows.Data;
using ChewsiPlugin.UI.Services;

namespace ChewsiPlugin.UI.Converters
{
    internal class ClientServiceStatusToBooleanConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length == 3 && values[0] is bool && values[1] is bool && values[2] is IClientAppService)
            {
                var vm = (IClientAppService) values[2];
                return vm.Initialized && !vm.IsLoadingClaims;
            }
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
