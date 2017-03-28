using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight.Threading;
using Microsoft.Practices.ServiceLocation;
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
            var arg = e.Args.Any() ? e.Args.First() : null;
            if (arg == "initDentrix")
            {
                Logger.Info("Initializing Dentrix API key");

                // initialize Dentrix API to register user
                var api = new DentrixApi(new MessageBoxDialogService());
                api.Unload();
                Shutdown();
            }
            else 
            {
                ViewModelLocator.InitContainer();
                var vm = ServiceLocator.Current.GetInstance<MainViewModel>();
                // when 'init' parameter exists - display settings view; try to fill Address, State and TIN
                vm.Initialize(arg == "init");
                var window = new MainWindow
                {
                    DataContext = ServiceLocator.Current.GetInstance<MainViewModel>()
                };
                window.Show();
            }
        }
    }
}
