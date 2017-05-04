using System;
using System.Collections.Generic;

namespace ChewsiPlugin.Api.Repository
{
    public interface IRepository
    {
        void AddAppointment(Appointment item);
        List<Appointment> GetAppointments();
        Appointment GetAppointmentById(string id);
        bool AppointmentExists(string id);
        T GetSettingValue<T>(string key);
        void SaveSetting(string key, object value);
        void UpdateAppointment(Appointment item);
        void BulkDeleteAppointments(List<string> ids);
        bool Ready { get; }
        void Initialize();
    }
}