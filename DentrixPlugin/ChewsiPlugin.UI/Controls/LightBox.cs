using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChewsiPlugin.UI.Controls
{
    [TemplatePart(Name = "PART_Container")]
    internal class LightBox : HeaderedContentControl
    {
        private static readonly RoutedUICommand EscapeKeyCommand = new RoutedUICommand("EscapeKeyCommand",
            "EscapeKeyCommand", typeof (LightBox), new InputGestureCollection(new InputGesture[]
            {new KeyGesture(Key.Escape, ModifierKeys.None, "Close")}));

        public LightBox()
        {
            CommandBindings.Add(new CommandBinding(EscapeKeyCommand, (sender, e) => { CloseCommand.Execute(null); }));
        }

        public static readonly DependencyProperty ButtonTextProperty = DependencyProperty.Register(
            "ButtonText", typeof (string), typeof (LightBox), new PropertyMetadata("Ok"));

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

        public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register(
            "ShowCloseButton", typeof (bool), typeof (LightBox), new PropertyMetadata(true));

        public bool ShowCloseButton
        {
            get { return (bool) GetValue(ShowCloseButtonProperty); }
            set { SetValue(ShowCloseButtonProperty, value); }
        }

        public static readonly DependencyProperty ShowHeaderAndFooterProperty = DependencyProperty.Register(
            "ShowHeaderAndFooter", typeof (bool), typeof (LightBox), new PropertyMetadata(true));

        public bool ShowHeaderAndFooter
        {
            get { return (bool) GetValue(ShowHeaderAndFooterProperty); }
            set { SetValue(ShowHeaderAndFooterProperty, value); }
        }
    }
}