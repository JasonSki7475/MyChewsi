using System;
using OpenDentBusiness;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class AppointmentInfo
    {
        public ApptStatus AptStatus { get; set; }
        public long InsPlan1 { get; set; }
        public long InsPlan2 { get; set; }
        public long PatNum { get; set; }
        public long ProvNum { get; set; }
        public DateTime AptDateTime { get; set; }
    }
}
