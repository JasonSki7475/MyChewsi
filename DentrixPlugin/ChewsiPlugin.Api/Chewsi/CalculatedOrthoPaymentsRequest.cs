namespace ChewsiPlugin.Api.Chewsi
{
    public class CalculatedOrthoPaymentsRequest
    {
        public string DateOfService { get; set; }
        public string ProcedureCode { get; set; }
        public string ProcedureCharge { get; set; }
        public double DownPaymentAmount { get; set; }
        public int NumberOfPayments { get; set; }
        public string FirstMonthlyPaymentDate { get; set; }
    }
}