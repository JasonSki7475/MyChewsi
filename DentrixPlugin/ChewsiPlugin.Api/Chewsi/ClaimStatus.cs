using System;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Chewsi
{
    [DataContract]
    public class ClaimStatus
    {
        [DataMember]
        public DateTime PostedOnDate { get; set; }

        [DataMember]
        public string SubscriberFirstName { get; set; }

        [DataMember]
        public string PatientFirstName { get; set; }

        [DataMember]
        public string PatientLastName { get; set; }

        [DataMember]
        public string Claim_Nbr { get; set; }

        [DataMember]
        public string ChewsiID { get; set; }

        [DataMember]
        public string MessageToDisplay { get; set; }

        [DataMember]
        public string Status { get; set; }

        /// <summary>
        /// Set by the plugin
        /// </summary>
        [DataMember]
        public string ProviderId { get; set; }
    }
}