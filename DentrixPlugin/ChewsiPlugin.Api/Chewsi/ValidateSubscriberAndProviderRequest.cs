namespace ChewsiPlugin.Api.Chewsi
{
    public class ValidateSubscriberAndProviderRequest
    {
        public string ChewsiID { get; set; }
        public string TIN { get; set; }
        public string NPI { get; set; }
        public string RenderingAddress1 { get; set; }
        public string RenderingAddress2 { get; set; }
        public string RenderingState { get; set; }
        public string RenderingCity { get; set; }
        public string RenderingZip { get; set; }
        public string SubscriberFirstName { get; set; }
        public string SubscriberLastName { get; set; }
        public string SubscriberDOB { get; set; }
    }
}