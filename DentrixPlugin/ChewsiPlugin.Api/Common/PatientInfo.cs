using System;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    [Serializable]
    public class PatientInfo: MarshalByRefObject
    {
        [DataMember]
        public string PatientFirstName { get; set; }

        [DataMember]
        public string PatientLastName { get; set; }

        [DataMember]
        public string SubscriberFirstName { get; set; }

        [DataMember]
        public string SubscriberLastName { get; set; }

        [DataMember]
        public DateTime? BirthDate { get; set; }

        [DataMember]
        public string ChewsiId { get; set; }
    }
}
