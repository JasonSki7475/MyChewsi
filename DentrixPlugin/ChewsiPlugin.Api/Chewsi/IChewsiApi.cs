using System;
using System.Collections.Generic;
using System.IO;

namespace ChewsiPlugin.Api.Chewsi
{
    public interface IChewsiApi
    {
        string RegisterPlugin(RegisterPluginRequest request);
        void UpdatePluginRegistration(UpdatePluginRegistrationRequest request);
        void ProcessClaim(string id, ProviderInformation provider, SubscriberInformation subscriber, List<ClaimLine> procedures, DateTime pmsModifiedDate);
        ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider,
            ProviderAddressInformation providerAddress, SubscriberInformation subscriber);
        Request835DownloadsResponse Get835Downloads(Request835Downloads request);
        ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request);
        Stream DownloadFile(DownoadFileRequest request);
        void Initialize(string token, bool useProxy, string proxyAddress, int proxyPort, string proxyUserName, string proxyPassword);
        bool StorePluginClientRowStatus(PluginClientRowStatus request);
        List<PluginClientRowStatus> RetrievePluginClientRowStatuses(string tin);
        CalculatedOrthoPaymentsResponse GetCalculatedOrthoPayments(CalculatedOrthoPaymentsRequest request);
        List<OrthoPaymentPlanHistoryResponse> GetOrthoPaymentPlanHistory(string tin);
    }
}