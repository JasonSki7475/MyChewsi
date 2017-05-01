using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    internal interface IClientAppService
    {
        void ValidateAndSubmitClaim(string appointmentId, DateTime date, string providerId, string patientId, DateTime pmsModifiedDate);
        void DownloadFile(string documentId, string postedDate, bool downloadReport);
        List<DownloadItemViewModel> GetDownloads();
        ObservableCollection<ClaimItemViewModel> ClaimItems { get; }
        bool Initialized { get; }
        bool IsLoadingAppointments { get; set; }
        void OpenSettings();
        void Initialize();
        void DeleteAppointment(string id);
        void RefreshAppointments();
        void SaveSettings(SettingsDto settingsDto);
    }
}
