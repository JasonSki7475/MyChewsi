namespace ChewsiPlugin.Api.Repository
{
    public enum AppointmentState
    {
        TreatmentInProgress = 0,
        TreatmentCompleted = 1,
        ValidationCompleted = 2,
        ValidationErrorUnrecoverable = 3,
        ValidationError = 4,
        PendingStatusLookup = 5,
        Deleted = 6,
        PaymentCompleted = 7
    }
}