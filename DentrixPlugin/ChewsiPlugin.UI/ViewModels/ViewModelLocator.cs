using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;

namespace ChewsiPlugin.UI.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    internal class ViewModelLocator
    {
        public static void InitContainer()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
            SimpleIoc.Default.Register<IClientDialogService, DialogService.DialogService>(true);
            var dialogService = SimpleIoc.Default.GetInstance<IClientDialogService>();
            SimpleIoc.Default.Register<IDialogService>(() => dialogService);
            SimpleIoc.Default.Register<IChewsiApi, ChewsiApi>();
            SimpleIoc.Default.Register<IConnectViewModel, ConnectViewModel>();
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<IRepository, Repository>();
            SimpleIoc.Default.Register<IClientAppService, ClientAppService>();
            SimpleIoc.Default.Register<IClientCallback, CallbackHandler>();
            SimpleIoc.Default.Register<ISettingsViewModel, SettingsViewModel>();
            SimpleIoc.Default.Register<ILauncherService, LauncherService>();
        }

        public ISettingsViewModel SettingsViewModel => SimpleIoc.Default.GetInstance<ISettingsViewModel>();

        public MainViewModel MainViewModel => SimpleIoc.Default.GetInstance<MainViewModel>();

        public IConnectViewModel ConnectViewModel => SimpleIoc.Default.GetInstance<IConnectViewModel>();
    }
}