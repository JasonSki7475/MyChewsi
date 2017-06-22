using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace ChewsiPlugin.UI.Converters
{
    internal class HighlightCalculatedPaymentsToFlowDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (string)value;
            return val.FormatDocument(
            new Tuple<string, Func<Inline, Inline>>(@"(?:(\d+,?\s?))+", p =>
            {
                p.FontStyle = FontStyles.Italic;
                p.FontWeight = FontWeights.SemiBold;
                return p;
            }), 
            new Tuple<string, Func<Inline, Inline>>(@"Note:", p =>
            {
                p.FontWeight = FontWeights.Bold;
                return p;
            }),
            new Tuple<string, Func<Inline, Inline>>(@"today's charges", p => new Underline(p)));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
