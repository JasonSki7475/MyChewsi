namespace ChewsiPlugin.Api.Repository
{
    public enum AppointmentState
    {
        TreatmentInProgress = 0,
        TreatmentCompleted = 1,

        /// <summary>
        /// Pending status lookup
        /// </summary>
        ValidationCompletedAndClaimSubmitted = 2,

        ValidationError = 3,
        Deleted = 4,
        PaymentCompleted = 5,
        SubscriberDeniesPayment = 6
    }
}