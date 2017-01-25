namespace DentrixPlugin.Api.DentrixApi
{
    public class Appointment
    {
        public string Appointment_date { get; set; }
        public string Provider_id { get; set; }
        public string Patient_name { get; set; }
        public string Patient_id { get; set; }
        public string Amount { get; set; }
        public string Status_id { get; set; }

        /// <summary>
        /// IDs for the appointment reason.Each ID links to Procedure Codes record (see proccodeid in v_proccodes)
        /// </summary>
        public string Codeid1 { get; set; }

        public string Codeid2 { get; set; }
        public string Codeid3 { get; set; }
    }
}
