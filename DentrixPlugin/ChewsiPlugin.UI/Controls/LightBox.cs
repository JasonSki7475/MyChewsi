using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChewsiPlugin.UI.Controls
{
    [TemplatePart(Name = "PART_Container")]
    public class LightBox : HeaderedContentControl
    {
        public static readonly DependencyProperty ButtonTextProperty = DependencyProperty.Register(
            "ButtonText", typeof (string), typeof (LightBox), new PropertyMetadata(default(string)));

        public string ButtonText
        {
            get { return (string) GetValue(ButtonTextProperty); }
            set { SetValue(ButtonTextProperty, value); }
        }

        public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
            "CloseCommand", typeof (ICommand), typeof (LightBox), new PropertyMetadata(default(ICommand)));

        public ICommand CloseCommand
        {
            get { return (ICommand) GetValue(CloseCommandProperty); }
            set { SetValue(CloseCommandProperty, value); }
        }

        public static readonly DependencyProperty ButtonCommandProperty = DependencyProperty.Register(
            "ButtonCommand", typeof (ICommand), typeof (LightBox), new PropertyMetadata(default(ICommand)));

        public ICommand ButtonCommand
        {
            get { return (ICommand) GetValue(ButtonCommandProperty); }
            set { SetValue(ButtonCommandProperty, value); }
        }
    }
}