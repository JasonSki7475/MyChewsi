using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;

namespace ChewsiPlugin.UI.ViewModels
{
    internal interface ISettingsViewModel
    {
        //string Address1 { get; set; }
        //string Address2 { get; set; }
        //bool CanChangeStartPms { get; }
        //bool NeedsPath { get; }
        //string Path { get; set; }
        //string ProxyAddress { get; set; }
        //string ProxyLogin { get; set; }
        //string ProxyPassword { get; set; }
        //int ProxyPort { get; set; }
        //Settings.PMS.Types SelectedType { get; set; }
        //ICommand SelectPathCommand { get; }
        //bool StartLauncher { get; set; }
        //bool StartPms { get; set; }
        //string State { get; set; }
        //string Tin { get; set; }
        //Settings.PMS.Types[] Types { get; }
        //bool UseProxy { get; set; }

        ICommand CloseCommand { get; }
        bool IsVisible { get; }
        ICommand SaveCommand { get; }

        void Fill(string addressLine1, string addressLine2, string state, string tin, bool startLauncher, string proxyAddress, int proxyPort);
        void Show(Action onClose);

        void InjectAppServiceAndInit(IClientAppService appService, SettingsDto settings);
    }
}