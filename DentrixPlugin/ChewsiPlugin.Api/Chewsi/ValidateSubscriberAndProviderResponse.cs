namespace ChewsiPlugin.Api.Chewsi
{
    public class ValidateSubscriberAndProviderResponse
    {
        public string ProviderID { get; set; }
        public string SubscriberID { get; set; }
        public string SubscriberValidationStatus { get; set; }
        public string SubscriberValidationMessage { get; set; }
        public string ProviderValidationStatus { get; set; }
        public string ProviderValidationMessage { get; set; }

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
         
        public string Message { get; set; }
*/
// TODO
        public bool ValidationPassed
        {
            get { return SubscriberValidationMessage == "Valid" && ProviderValidationMessage == "Valid"; }
        }
    }
}
