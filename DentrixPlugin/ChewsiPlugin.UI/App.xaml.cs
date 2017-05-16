using System;
using System.Windows;
using System.Windows.Threading;
using ChewsiPlugin.UI.Services;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Threading;
using NLog;

namespace ChewsiPlugin.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static App()
        {
            DispatcherHelper.Initialize();
        }

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += ApplicationDomainUnhandledException;
            DispatcherUnhandledException += ApplicationDispatcherUnhandledException;
        }

        private void ApplicationDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "Application dispatcher unhandled exception has been thrown");
        }

        private void ApplicationDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            /*
            var dialogService = SimpleIoc.Default.GetInstance<IDialogService>();
            dialogService.Show("Unexpected error occured: " + exception.Message + (e.IsTerminating ? " Application will be closed." : ""), "Error", () => Current.Shutdown());
            */
            Logger.Error(exception,
                e.IsTerminating
                    ? "Application domain unhandled exception has been thrown, application will be terminated"
                    : "Application domain unhandled exception has been thrown");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ViewModelLocator.InitContainer();
            var window = new MainWindow();
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            var vm = SimpleIoc.Default.GetInstance<IClientAppService>();
            vm.Dispose();
        }
    }
}
