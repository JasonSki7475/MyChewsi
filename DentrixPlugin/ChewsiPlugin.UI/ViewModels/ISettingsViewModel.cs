using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;

namespace ChewsiPlugin.UI.ViewModels
{
    internal interface ISettingsViewModel
    {
        string Address1 { get; set; }
        string Address2 { get; set; }
        bool CanChangeStartPms { get; }
        ICommand CloseCommand { get; }
        bool IsVisible { get; }
        bool NeedsPath { get; }
        string Path { get; set; }
        string ProxyAddress { get; set; }
        string ProxyLogin { get; set; }
        string ProxyPassword { get; set; }
        int ProxyPort { get; set; }
        ICommand SaveCommand { get; }
        Settings.PMS.Types SelectedType { get; set; }
        ICommand SelectPath { get; }
        bool StartLauncher { get; set; }
        bool StartPms { get; set; }
        string State { get; set; }
        string Tin { get; set; }
        Settings.PMS.Types[] Types { get; }
        bool UseProxy { get; set; }

        void Fill(string addressLine1, string addressLine2, string state, string tin, bool startLauncher, string proxyAddress, int proxyPort);
        void Show(Action onClose);

        void InjectAppServiceAndInit(IAppService appService);
    }
}