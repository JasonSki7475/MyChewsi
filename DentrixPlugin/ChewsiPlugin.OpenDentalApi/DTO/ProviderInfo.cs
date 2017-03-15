using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class ProviderInfo
    {
        public string SSN { get; set; }
        public string StateWhereLicensed { get; set; }
        public string NationalProvID { get; set; }
    }
}
