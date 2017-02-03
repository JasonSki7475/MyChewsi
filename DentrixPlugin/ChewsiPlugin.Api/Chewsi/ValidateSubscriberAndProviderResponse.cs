namespace ChewsiPlugin.Api.Chewsi
{
    public class ValidateSubscriberAndProviderResponse
    {
        public string ProviderId { get; set; }
        public string SubscriberId { get; set; }

        /*
         *      •	Valid
                    For when the provider’s information matches our data
                •	Provider not found
                   For when we cannot find the provider based on the information provided 
                •	Provider no longer active
                   For when the provider is no longer active 
                •	Inconclusive validation. Confirm at office.
                   For when we have a match on some, but not all, of the following values:
                      -TIN 
                      -NPI
                      -License & State
         */
        public string Message { get; set; }

        public bool ValidationPassed
        {
            get { return Message == "Valid"; }
        }
    }
}
