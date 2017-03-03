using System;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI.Services
{
    public interface IAppService
    {
        bool Initialized { get; }
        void SaveSettings(SettingsDto settingsDto);
        SettingsDto GetSettings();
        void InitializeChewsiApi();
        void UpdatePluginRegistration();
        ValidateSubscriberAndProviderResponse ValidateClaim(string providerId, string patientId,
            out ProviderInformation providerInformation, out SubscriberInformation subscriberInformation,
            out Provider provider);
        void SubmitClaim(string patientId, ProviderInformation providerInformation,
            SubscriberInformation subscriberInformation, Provider provider);
        void UpdateCachedClaim(string chewsiId, DateTime date, AppointmentState state, string statusText);
    }
}