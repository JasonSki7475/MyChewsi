using System;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.OpenDentalApi
{
    internal class Appointment : IAppointment
    {
        public DateTime Date { get; set; }
        public string ProviderId { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
        public string InsuranceId { get; set; }
        public bool IsCompleted { get; set; }
    }
}
