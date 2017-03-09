using System;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IAppointment
    {
        DateTime Date { get; set; }
        bool IsCompleted { get; }
        string PatientId { get; set; }
        string PatientName { get; set; }
        string ProviderId { get; set; }
        string ChewsiId { get; set; }
    }
}