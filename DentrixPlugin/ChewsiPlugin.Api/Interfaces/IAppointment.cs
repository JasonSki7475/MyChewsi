using System;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IAppointment
    {
        DateTime Date { get; set; }
        string Id { get; set; }
        string PatientId { get; set; }
        string PatientName { get; set; }
        string ProviderId { get; set; }
        string ChewsiId { get; set; }
    }
}