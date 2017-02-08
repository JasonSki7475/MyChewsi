using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class PatientInfo
    {
        public string LName { get; set; }
        public string FName { get; set; }
        public DateTime Birthdate { get; set; }
    }
}
