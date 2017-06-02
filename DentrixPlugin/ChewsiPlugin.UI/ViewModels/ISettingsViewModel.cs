using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.Services;

namespace ChewsiPlugin.UI.ViewModels
{
    internal interface ISettingsViewModel
    {
        ICommand CloseCommand { get; }
        bool IsVisible { get; }
        ICommand SaveCommand { get; }
        void Show(Action onClose);
        void InjectAppServiceAndInit(IClientAppService appService, SettingsDto settings, string serverAddress, bool startLauncher, bool isClient);
    }
}