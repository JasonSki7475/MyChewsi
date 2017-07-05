using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class CalculatedPaymentsDto
    {
        [DataMember]
        public string ChewsiMonthlyFee { get; set; }

        [DataMember]
        public string SubscribersReoccuringMonthlyCharge { get; set; }

        [DataMember]
        public string TotalProviderReimbursement { get; set; }

        [DataMember]
        public string TotalProviderSubmittedCharge { get; set; }

        [DataMember]
        public string TotalSubscriberCharge { get; set; }
    }
}
