using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class SubscriberInfo
    {
        public string ChewsiId { get; set; }
        public PatientInfo PatientInfo { get; set; }
    }
}
