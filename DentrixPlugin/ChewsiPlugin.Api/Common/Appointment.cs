using System;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class Appointment
    {
        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public DateTime PmsModifiedDate { get; set; }

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string ProviderId { get; set; }

        [DataMember]
        public string ChewsiId { get; set; }

        [DataMember]
        public string PatientName { get; set; }

        [DataMember]
        public string PatientId { get; set; }
    }
}