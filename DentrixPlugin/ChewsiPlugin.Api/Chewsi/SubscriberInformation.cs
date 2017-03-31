using System;

namespace ChewsiPlugin.Api.Chewsi
{
    public class SubscriberInformation
    {
        public string PatientFirstName { get; set; }
        public string PatientLastName { get; set; }
        public string SubscriberFirstName { get; set; }
        public string SubscriberLastName { get; set; }
        public DateTime? SubscriberDateOfBirth { get; set; }

        /// <summary>
        /// Chewsi Id
        /// </summary>
        public string Id { get; set; }
    }
}
