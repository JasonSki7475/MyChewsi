using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ClaimLine
    {
        public ClaimLine(DateTime dateOfService, string procedureCode, double procedureCharge)
        {
            DateOfService = dateOfService.ToString("G");
            ProcedureCode = procedureCode;
            ProcedureCharge = procedureCharge.ToString("F");
        }

        public string DateOfService { get; private set; }
        public string ProcedureCode { get; private set; }
        public string ProcedureCharge { get; private set; }
    }
}
