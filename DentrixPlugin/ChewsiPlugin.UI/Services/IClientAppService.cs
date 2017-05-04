using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    internal interface IClientAppService
    {
        void ValidateAndSubmitClaim(string id);
        void DownloadFile(string documentId, string postedDate, bool downloadReport);
        List<DownloadItemViewModel> GetDownloads();
        ObservableCollection<ClaimItemViewModel> ClaimItems { get; }
        bool Initialized { get; }
        bool IsLoadingClaims { get; }
        void OpenSettings();
        void Initialize();
        void DeleteAppointment(string id);
        void ReloadClaims(bool force);
        void SaveSettings(SettingsDto settingsDto);

        #region Server callbacks
        void LockClaim(string id);
        void UnlockClaim(string id);
        void SetClaims(List<ClaimDto> claims);
        #endregion
    }
}
