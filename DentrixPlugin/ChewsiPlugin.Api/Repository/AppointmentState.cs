namespace ChewsiPlugin.Api.Repository
{
    public enum AppointmentState
    {
        TreatmentCompleted = 0,

        /// <summary>
        /// Pending status lookup
        /// </summary>
        ValidationCompletedAndClaimSubmitted = 1,

        ValidationError = 2,
        ValidationErrorNoResubmit = 3,

        Deleted = 4
    }
}