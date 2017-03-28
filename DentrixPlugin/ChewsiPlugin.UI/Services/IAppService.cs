using System;
using System.Collections.ObjectModel;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    public interface IAppService
    {
        bool Initialized { get; }
        void SaveSettings(SettingsDto settingsDto);
        SettingsDto GetSettings();
        void InitializeChewsiApi();
        void UpdatePluginRegistration();
        void RefreshAppointments(bool loadFromPms);
        ObservableCollection<ClaimItemViewModel> ClaimItems { get; }
        bool IsProcessingPayment { get; }
        bool IsLoadingAppointments { get; }
        bool AppointmentsLoaded { get; }
        void DeleteAppointment(string chewsiId, DateTime date);
        void DeleteOldAppointments();
        event Action OnStartPaymentStatusLookup;
        void ValidateAndSubmitClaim(string chewsiId, DateTime date, string providerId, string patientId, Action callEndCallback);
        void StartPmsIfRequired();
        void DownloadFile(string documentId, string postedDate, bool downloadReport);
        Provider GetProvider();
    }
}