using System;
using System.Collections.Generic;

namespace ChewsiPlugin.Api.Repository
{
    public interface IRepository
    {
        void AddAppointment(Appointment item);
        Appointment GetAppointmentByChewsiIdAndDate(string chewsiId, DateTime date);
        List<Appointment> GetAppointments();
        T GetSettingValue<T>(string key);
        void SaveSetting(string key, object value);
        void UpdateAppointment(Appointment item);
        void BulkDeleteAppointments(List<int> ids);
        bool Initialized { get; }
        void Initialize();
    }
}