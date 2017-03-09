using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ClaimStatus
    {
        public DateTime PostedOnDate { get; set; }
        public string SubscriberFirstName { get; set; }
        public string PatientFirstName { get; set; }
        public string clm_nbr { get; set; }
        public string MessageToDisplay { get; set; }
        public ClaimStatusType Status { get; set; }
    }
}