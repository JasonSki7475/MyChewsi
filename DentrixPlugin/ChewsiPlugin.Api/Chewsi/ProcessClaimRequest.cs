using System.Collections.Generic;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ProcessClaimRequest
    {
        /// <summary>
        /// Practice Management System unique claim identifier
        /// </summary>
        public string PMS_ID { get; set; }

        public string ProviderID { get; set; }
        public string TIN { get; set; }
        public string NPI { get; set; }
        public string OfficeNbr { get; set; }
        public string SubscriberID { get; set; }
        public string SubscriberFirstName { get; set; }
        public string SubscriberLastName { get; set; }
        public string SubscriberDOB { get; set; }
        public string PIN { get; set; }
        public List<ClaimLine> ClaimLines { get; set; }
    }
}
