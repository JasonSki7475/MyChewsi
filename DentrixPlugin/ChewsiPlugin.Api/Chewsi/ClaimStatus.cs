using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ClaimStatus
    {
        public DateTime DateOfService { get; set; }
        public string SubscriberName { get; set; }
        public string PatientName { get; set; }
        public string clm_nbr { get; set; }
        public string MessageToDisplay { get; set; }
        public ClaimStatusType Status { get; set; }
    }
}
