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
    }
}
