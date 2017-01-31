namespace DentrixPlugin.Api.ChewsiApi
{
    public class ProviderInformationRequest
    {
        public string TIN { get; set; }
        public string NPI { get; set; }
        public string RenderingAddress { get; set; }
        public string RenderingState { get; set; }
        public string RenderingCity { get; set; }
        public string RenderingZip { get; set; }
    }
}
