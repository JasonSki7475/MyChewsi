namespace ChewsiPlugin.Api.Chewsi
{
    public class ReceiveMemberAuthorizationRequest
    {
        public string ChewsiID { get; set; }
        public string ClaimNumber { get; set; }
        public string Authorization { get; set; }
    }
}
