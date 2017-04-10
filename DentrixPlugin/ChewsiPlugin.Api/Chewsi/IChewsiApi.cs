using System.Collections.Generic;
using System.IO;

namespace ChewsiPlugin.Api.Chewsi
{
    public interface IChewsiApi
    {
        string RegisterPlugin(RegisterPluginRequest request);
        void UpdatePluginRegistration(UpdatePluginRegistrationRequest request);
        void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ClaimLine> procedures);
        ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider,
            ProviderAddressInformation providerAddress, SubscriberInformation subscriber);
        Request835DownloadsResponse Get835Downloads(Request835Downloads request);
        ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request);
        Stream DownloadFile(DownoadFileRequest request);
        void Initialize(string token, bool useProxy, string proxyAddress, int proxyPort, string proxyUserName, string proxyPassword);
    }
}