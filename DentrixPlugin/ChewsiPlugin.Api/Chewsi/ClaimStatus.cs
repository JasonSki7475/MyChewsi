using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ClaimStatus
    {
        public DateTime PostedOnDate { get; set; }
        public string SubscriberFirstName { get; set; }
        public string PatientFirstName { get; set; }
        public string PatientLastName { get; set; }
        public string Claim_Nbr { get; set; }
        public string ChewsiID { get; set; }
        public string MessageToDisplay { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// Set by the plugin
        /// </summary>
        public string ProviderId { get; set; }
    }
}