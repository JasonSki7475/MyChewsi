using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI
{
    public interface IAppLoader
    {
        IDentalApi GetDentalApi();
        bool IsInitialized();
        void RegisterPlugin(Settings.PMS.Types pmsType, string address1, string address2, string tin, string pmsVersion);
        void Initialize(Settings.PMS.Types pmsType, string pmsPath, string address1, string address2, string tin);
        // Now we use only short version of the UpdatePluginRegistration method (see below), full version will be called only if we allow user to open settings view and change other settings
        // void UpdatePluginRegistration(Settings.PMS.Types pmsType, string address1, string address2, string tin, string pmsVersion);
        void UpdatePluginRegistration(string pmsVersion);
    }
}