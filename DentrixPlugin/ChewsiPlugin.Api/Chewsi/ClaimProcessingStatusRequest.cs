namespace ChewsiPlugin.Api.Chewsi
{
    public class ClaimProcessingStatusRequest
    {
        public string TIN { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
    }
}
