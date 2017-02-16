using System.Collections.Generic;

namespace ChewsiPlugin.Api.Chewsi
{
    public interface IChewsiApi
    {
        string RegisterPlugin(RegisterPluginRequest request);
        void UpdatePluginRegistration(UpdatePluginRegistrationRequest request);
        void ProcessClaim(ProviderInformation provider, SubscriberInformation subscriber, List<ProcedureInformation> procedures);
        ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider, SubscriberInformation subscriber);
        Request835DownloadsResponse Get835Downloads(Request835Downloads request);
        void ReceiveMemberAuthorization(ReceiveMemberAuthorizationRequest request);
        ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request);
        string DownoadFile(DownoadFileRequest request);
    }
}