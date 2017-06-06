using System;

namespace ChewsiPlugin.Api.Repository
{
    public class SubmittedProcedure
    {
        public int Id { get; set; }
        public string PatientId { get; set; }
        public DateTime Date { get; set; }
        public string Code { get; set; }
        public double Amount { get; set; }
    }
}