namespace ChewsiPlugin.Api.Chewsi
{
    public class CalculatedOrthoPaymentsResponse
    {
        public string ChewsiMonthlyFee { get; set; }
        public string SubscribersReoccuringMonthlyCharge { get; set; }
        public string TotalProviderReimbursement { get; set; }
        public string TotalProviderSubmittedCharge { get; set; }
        public string TotalSubscriberCharge { get; set; }
        public string Note { get; set; }
    }
}
