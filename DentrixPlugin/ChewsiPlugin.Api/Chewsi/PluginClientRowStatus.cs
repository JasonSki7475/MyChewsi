namespace ChewsiPlugin.Api.Chewsi
{
    public class PluginClientRowStatus
    {
        public string TIN { get; set; }
        public string Status { get; set; }
        public string PMSClaimNbr { get; set; }
        public string PMSModifiedDate { get; set; }

        public enum Statuses
        {
            S, // Submitted
            D // Deleted
        }
    }
}
