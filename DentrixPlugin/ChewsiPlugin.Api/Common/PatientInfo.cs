using System;

namespace ChewsiPlugin.Api.Common
{
    [Serializable]
    public class PatientInfo: MarshalByRefObject
    {
        public string PatientFirstName { get; set; }
        public string PatientLastName { get; set; }
        public string SubscriberFirstName { get; set; }
        public string SubscriberLastName { get; set; }
        public DateTime BirthDate { get; set; }
        public string ChewsiId { get; set; }
    }
}
