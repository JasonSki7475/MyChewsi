using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class PluginClientRowStatus
    {
        public string TIN { get; set; }
        public Statuses Status { get; set; }
        public string PMSClaimNbr { get; set; }
        public DateTime PMSModifiedDate { get; set; }

        public enum Statuses
        {
            S, // Submitted
            D // Deleted
        }
    }
}
