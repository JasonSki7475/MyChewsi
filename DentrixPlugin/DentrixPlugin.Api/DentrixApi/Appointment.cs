namespace DentrixPlugin.Api.DentrixApi
{
    public class Appointment
    {
        public string Appointment_date { get; set; }
        public string Provider_id { get; set; }
        public string Patient_name { get; set; }
        public string Patient_id { get; set; }
        public string Primary_insured_id { get; set; }
        public string Status_id { get; set; }

        public bool IsCompleted { get { return Status_id == "150"; } }
    }
}