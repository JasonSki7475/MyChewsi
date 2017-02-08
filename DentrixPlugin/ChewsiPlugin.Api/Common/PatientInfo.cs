using System;

namespace ChewsiPlugin.Api.Common
{
    [Serializable]
    public class PatientInfo: MarshalByRefObject
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
        //public string InsuranceId { get; set; }
    }
}
