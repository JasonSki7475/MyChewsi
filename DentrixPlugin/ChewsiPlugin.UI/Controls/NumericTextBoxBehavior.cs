using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChewsiPlugin.UI.Controls
{
    internal class NumericTextBoxBehavior
    {
        #region Enabled Property

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(NumericTextBoxBehavior),
                new FrameworkPropertyMetadata(EnabledChangedCallback));

        private static void EnabledChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TextBox textBox = d as TextBox;
            if (textBox != null)
            {
                textBox.PreviewTextInput += PreviewTextInput;
                DataObject.AddPastingHandler(textBox, TextBoxPasting);
            }
        }

        #endregion

        private static bool IsNumeric(string text)
        {
            Regex regex = new Regex("[^0-9]+");
            return !regex.IsMatch(text);
        }

        private static void PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsNumeric(e.Text);
        }

        private static void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsNumeric(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
