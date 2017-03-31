using System;

namespace ChewsiPlugin.Api.Repository
{
    public class Appointment
    {
        public int Id { get; set; }
        public string ChewsiId { get; set; }
        public DateTime DateTime { get; set; }
        public AppointmentState State { get; set; }
        public string StatusText { get; set; }
        public string PatientName { get; set; }
        public string SubscriberFirstName { get; set; }
        public string ProviderId { get; set; }
        public string PatientId { get; set; }
    }
}
