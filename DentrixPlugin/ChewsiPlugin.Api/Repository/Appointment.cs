using System;

namespace ChewsiPlugin.Api.Repository
{
    public class Appointment
    {
        public string Id { get; set; }
        public string ChewsiId { get; set; }
        public DateTime DateTime { get; set; }
        public bool Deleted { get; set; }
    }
}
