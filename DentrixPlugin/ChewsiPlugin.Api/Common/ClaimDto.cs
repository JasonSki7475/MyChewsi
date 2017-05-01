﻿using System;
using System.Runtime.Serialization;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class ClaimDto
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string ProviderId { get; set; }

        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public string PatientName { get; set; }

        [DataMember]
        public string ChewsiId { get; set; }

        [DataMember]
        public AppointmentState State { get; set; }

        [DataMember]
        public string StatusText { get; set; }

        [DataMember]
        public string PatientId { get; set; }

        [DataMember]
        public string SubscriberFirstName { get; set; }

        [DataMember]
        public bool IsClaimStatus { get; set; }

        [DataMember]
        public string ClaimNumber { get; set; }

        [DataMember]
        public DateTime PmsModifiedDate { get; set; }
    }
}