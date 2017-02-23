using ChewsiPlugin.Api.Interfaces;
namespace ChewsiPlugin.UI.Services
{
    public interface IAppService
    {
        IDentalApi DentalApi { get; }
        bool Initialized { get; }
        void SaveSettings(SettingsDto settingsDto);
        SettingsDto GetSettings();
        void InitializeChewsiApi();
        void UpdatePluginRegistration();
    }
}