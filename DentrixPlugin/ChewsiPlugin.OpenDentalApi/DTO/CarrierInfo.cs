using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class CarrierInfo
    {
        public string CarrierName { get; set; }
        public long CarrierNum { get; set; }
    }
}
