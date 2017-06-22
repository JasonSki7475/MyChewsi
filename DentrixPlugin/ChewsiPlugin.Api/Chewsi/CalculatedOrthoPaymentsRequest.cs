namespace ChewsiPlugin.Api.Chewsi
{
    public class CalculatedOrthoPaymentsRequest
    {
        public string DateOfService { get; set; }
        public string ProcedureCode { get; set; }
        public string ProcedureCharge { get; set; }
        public int DownPaymentAmount { get; set; }
        public string NumberOfPayments { get; set; }
        public string FirstMonthlyPaymentDate { get; set; }
    }
}