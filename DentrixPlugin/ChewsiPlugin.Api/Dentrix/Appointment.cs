using System;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.Api.Dentrix
{
    internal class Appointment : IAppointment
    {
        public DateTime Date { get; set; }
        public string ProviderId { get; set; }
        public string ChewsiId { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
    }
}