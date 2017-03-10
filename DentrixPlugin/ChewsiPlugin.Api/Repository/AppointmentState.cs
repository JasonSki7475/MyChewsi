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

        ValidationErrorUnrecoverable = 3,
        ValidationError = 4,
        Deleted = 5,
        PaymentCompleted = 6
    }
}