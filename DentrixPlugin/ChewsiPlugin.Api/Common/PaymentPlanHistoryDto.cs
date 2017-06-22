using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class PaymentPlanHistoryDto
    {
        [DataMember]
        public DateTime PostedOn { get; set; }

        [DataMember]
        public string ChewsiId { get; set; }

        [DataMember]
        public string PatientFirstName { get; set; }

        [DataMember]
        public string Provider { get; set; }

        [DataMember]
        public string PaymentSchedule { get; set; }

        [DataMember]
        public string LastPaymentOn { get; set; }

        [DataMember]
        public List<PaymentPlanHistoryItemDto> Items { get; set; }

        [DataMember]
        public string BalanceRemaining { get; set; }

        [DataMember]
        public string NextPaymentOn { get; set; }
    }

    [DataContract]
    public class PaymentPlanHistoryItemDto
    {
        [DataMember]
        public string PaymentSchedule { get; set; }

        [DataMember]
        public string PaymentMadeOn { get; set; }

        [DataMember]
        public string PatientPaymentOf { get; set; }

        [DataMember]
        public string ChewsiFeeAmount { get; set; }

        [DataMember]
        public string ProviderReceives { get; set; }
    }
}
