namespace ChewsiPlugin.Api.Chewsi
{
    public class ValidateSubscriberAndProviderResponse
    {
        public string ProviderID { get; set; }
        public string SubscriberID { get; set; }
        public string OfficeNumber { get; set; }

        /// <summary>
        /// Possible statuses: Valid, Subscriber Not Found, Subscriber No Longer Active
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
            get { return SubscriberValidationStatus == "Valid" && ProviderValidationStatus == "Valid"; }
        }

        public bool SubscriberNoLongerActive
        {
            get { return SubscriberValidationStatus == "Subscriber No Longer Active"; }
        }

        public bool ProviderNotFound
        {
            get { return ProviderValidationStatus == "Provider Not Found"; }
        }
    }
}
