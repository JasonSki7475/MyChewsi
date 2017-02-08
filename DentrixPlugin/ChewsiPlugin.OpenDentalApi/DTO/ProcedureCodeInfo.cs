using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class ProcedureCodeInfo
    {
        public string ProcCode { get; set; }
        public long CodeNum { get; set; }
    }
}
