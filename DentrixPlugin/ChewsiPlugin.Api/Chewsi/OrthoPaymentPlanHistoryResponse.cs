namespace ChewsiPlugin.Api.Chewsi
{
    public class OrthoPaymentPlanHistoryResponse
    {
        public string BalanceRemaining { get; set; }
        public string ChewsiID { get; set; }
        public string Clm_Nbr { get; set; }
        public string Clm_Seq { get; set; }
        public string LastPaymentOn { get; set; }
        public string NextPaymentOn { get; set; }
        public string PatientFirstName { get; set; }
        public string PaymentSchedule { get; set; }
        public string PostedOn { get; set; }
        public PaymentPlanItem[] Items { get; set; }
    }

    public class PaymentPlanItem
    {
        public string ChewsiFeeAmount { get; set; }
        public string Clm_Nbr { get; set; }
        public string Clm_Seq { get; set; }
        public string PatientPaymentOf { get; set; }
        public string PaymentMadeOn { get; set; }
        public string PaymentSchedule { get; set; }
        public string ProviderReceives { get; set; }
    }
}
