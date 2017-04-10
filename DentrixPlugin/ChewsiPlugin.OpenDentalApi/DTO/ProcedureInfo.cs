using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class ProcedureInfo
    {
        public double ProcFee { get; set; }
        public long CodeNum { get; set; }
        public DateTime ProcDate { get; set; }
        public int ProcStatus { get; set; }

        public bool IsCompleted => ProcStatus == 2;
    }
}
