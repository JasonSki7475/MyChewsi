using System;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.EaglesoftApi
{
    public class Appointment : IAppointment
    {
        public DateTime Date { get; set; }
        public string ProviderId { get; set; }
        public string ChewsiId { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
    }
}
