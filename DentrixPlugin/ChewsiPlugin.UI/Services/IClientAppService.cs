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
        void ValidateAndSubmitClaim(string id, double downPayment, int numberOfPayments);
        void DownloadFile(string documentId, string postedDate, bool downloadReport);
        List<DownloadItemViewModel> GetDownloads();
        List<PaymentPlanHistoryViewModel> GetPayments();
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
        CalculatedPaymentsDto GetCalculatedPayments(string id, double downPayment, int numberOfPayments, DateTime firstMonthlyPaymentDate);

        #region Server callbacks
        void LockClaim(string id);
        void UnlockClaim(string id);
        void SetClaims(List<ClaimDto> claims);
        #endregion
    }
}
