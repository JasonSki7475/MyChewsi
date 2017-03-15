using System;
using ChewsiPlugin.Api.Interfaces;
using NLog;

namespace ChewsiPlugin.Api.Dentrix
{
    internal class Appointment : IAppointment
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public DateTime Date { get; set; }
        public string ProviderId { get; set; }
        public string ChewsiId { get; set; }
        public string PatientName { get; set; }
        public string PatientId { get; set; }
        public string StatusId { get; set; }

        public bool IsCompleted
        {
            get
            {
                Logger.Debug($"Appointment {ChewsiId} has status {StatusId}");
                return StatusId == "-106" || StatusId == "150";
            }
        }
    }
}