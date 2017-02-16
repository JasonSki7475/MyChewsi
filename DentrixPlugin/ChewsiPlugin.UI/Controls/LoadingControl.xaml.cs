using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChewsiPlugin.UI.Controls
{
    /// <summary>
    /// Interaction logic for LoadingControl.xaml
    /// </summary>
    public partial class LoadingControl
    {
        private readonly DispatcherTimer _animationTimer;

        #region Constructor
        public LoadingControl()
        {
            InitializeComponent();

            _animationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle, Dispatcher)
            {
                Interval = new TimeSpan(0, 0, 0, 0, 50)
            };
        }
        #endregion

        #region Private methods
        private void Start()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _animationTimer.Tick += HandleAnimationTick;
            _animationTimer.Start();
        }

        private void Stop()
        {
            _animationTimer.Stop();
            Mouse.OverrideCursor = Cursors.Arrow;
            _animationTimer.Tick -= HandleAnimationTick;
        }

        private void HandleAnimationTick(object sender, EventArgs e)
        {
            SpinnerRotate.Angle = (SpinnerRotate.Angle + 36) % 360;
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            const double step = Math.PI * 2 / 7.0;

            SetPosition(C0, 0.0, step);
            SetPosition(C1, 1.0, step);
            SetPosition(C2, 2.0, step);
            SetPosition(C3, 3.0, step);
            SetPosition(C4, 4.0, step);
            SetPosition(C5, 5.0, step);
            SetPosition(C6, 6.0, step);
        }

        private void SetPosition(Ellipse ellipse, double posOffSet, double step)
        {
            var offset = 80.0/2.0 - 12.0/2;
            ellipse.SetValue(Canvas.LeftProperty, offset * (1+ Math.Sin(posOffSet*step)));
            ellipse.SetValue(Canvas.TopProperty, offset * (1+Math.Cos(posOffSet*step)));
        }

        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void HandleVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var isVisible = (bool)e.NewValue;
            if (isVisible)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
        #endregion
    }
}
