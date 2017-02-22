namespace ChewsiPlugin.Api.Chewsi
{
    public class ValidateSubscriberAndProviderResponse
    {
        public string ProviderID { get; set; }
        public string SubscriberID { get; set; }
        public string OfficeNumber { get; set; }
        
        /// <summary>
        /// Possible statuses: Valid, Subscriber Not Found
        /// </summary>
        public string SubscriberValidationStatus { get; set; }
        public string SubscriberValidationMessage { get; set; }

        /// <summary>
        /// Possible statuses: Valid, Provider Not Found
        /// </summary>
        public string ProviderValidationStatus { get; set; }
        public string ProviderValidationMessage { get; set; }

        public bool ValidationPassed
        {
            get { return SubscriberValidationMessage == "Valid" && ProviderValidationMessage == "Valid"; }
        }
    }
}
