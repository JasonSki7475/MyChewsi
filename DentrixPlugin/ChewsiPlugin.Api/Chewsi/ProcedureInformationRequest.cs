using System.Collections.Generic;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ProcedureInformationRequest
    {
        public List<ProcedureInfo> Procedures { get; set; }
        public string SubscriberDateOfBirth { get; set; }
    }

    public class ProcedureInfo
    {
        public string DateOfServices { get; set; }
        public string ProcedureCode { get; set; }
        public string ProcedureCharge { get; set; }        
    }
}
