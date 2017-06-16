using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    internal interface IClientAppService : IDisposable
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
        Task<bool> ReloadClaims();
        void SaveSettings(SettingsDto settingsDto, string serverAddress, bool startLauncher);
        Task<bool> Connect(string serverAddress = null);
        void DeleteClaimStatus(string providerId, string chewsiId, DateTime date);

        #region Server callbacks
        void LockClaim(string id);
        void UnlockClaim(string id);
        void SetClaims(List<ClaimDto> claims);
        #endregion
    }
}
