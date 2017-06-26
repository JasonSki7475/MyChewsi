using System;
using System.Collections.Generic;
using System.ServiceModel;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IClientCallback))]
    public interface IServerAppService : IDisposable
    {
        [OperationContract]
        SettingsDto GetSettings();

        [OperationContract]
        bool SaveSettings(SettingsDto settingsDto);

        [OperationContract]
        bool DeleteAppointment(string id);

        [OperationContract]
        SubmitClaimResult ValidateAndSubmitClaim(string id, double downPayment, int numberOfPayments);

        [OperationContract]
        void ReloadClaims();
        
        [OperationContract]
        ServerState InitClient();

        [OperationContract]
        void DisconnectClient();
        
        [OperationContract]
        InitialSettingsDto GetInitialSettings();

        [OperationContract]
        bool Ping();

        [OperationContract]
        List<DownloadDto> GetDownloads();

        [OperationContract]
        List<PaymentPlanHistoryDto> GetPayments();

        [OperationContract]
        File835Dto DownloadFile(string documentType, string documentId, string postedDate);

        [OperationContract]
        string GetPmsExecutablePath();

        [OperationContract]
        bool DeleteClaimStatus(string providerId, string chewsiId, DateTime date);

        [OperationContract]
        CalculatedPaymentsDto GetCalculatedPayments(string id, double downPayment, int numberOfPayments, DateTime firstMonthlyPaymentDate);

        /// <summary>
        /// For tests, InitClient() should be used by clients
        /// </summary>
        ServerState GetState();
    }
}
