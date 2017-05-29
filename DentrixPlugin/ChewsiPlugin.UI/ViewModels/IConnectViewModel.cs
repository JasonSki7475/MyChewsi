using ChewsiPlugin.UI.Services;

namespace ChewsiPlugin.UI.ViewModels
{
    internal interface IConnectViewModel
    {
        void InjectAppServiceAndInit(IClientAppService appService);
        void Show(string address);
    }
}