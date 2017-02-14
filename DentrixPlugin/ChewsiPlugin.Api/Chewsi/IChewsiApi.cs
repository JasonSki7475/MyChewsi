namespace ChewsiPlugin.Api.Chewsi
{
    public interface IChewsiApi
    {
        void Initialize(InitializeRequest request);
        string ProcessClaim(ProviderInformationRequest provider, SubscriberInformationRequest subscriber, ProcedureInformationRequest procedure);
        void RequestClaimProcessingStatus(ProviderInformationRequest provider);
        ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformationRequest provider, SubscriberInformationRequest subscriber);
    }
}