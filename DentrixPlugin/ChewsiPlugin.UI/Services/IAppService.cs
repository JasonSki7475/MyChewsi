using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    internal interface IAppService
    {
        void Initialize(bool firstRun);
        bool Initialized { get; }
        void SaveSettings(SettingsDto settingsDto);
        SettingsDto GetSettings();
        void RefreshAppointments(bool loadFromPms, bool loadFromService);
        ObservableCollection<ClaimItemViewModel> ClaimItems { get; }
        bool IsLoadingAppointments { get; }
        void DeleteAppointment(string id);
        void ValidateAndSubmitClaim(string appointmentId, DateTime date, string providerId, string patientId, Action callEndCallback);
        void DownloadFile(string documentId, string postedDate, bool downloadReport);
        List<DownloadItemViewModel> GetDownloads();
    }
}