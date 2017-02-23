using System.Collections.Generic;

namespace ChewsiPlugin.Api.Repository
{
    public interface IRepository
    {
        void AddAppointment(Appointment item);
        Appointment GetAppointmentById(string id);
        IEnumerable<Appointment> GetAppointments();
        T GetSettingValue<T>(string key);
        void SaveSetting(string key, object value);
        void UpdateAppointment(Appointment item);
        bool Initialized { get; }
        void Initialize();
    }
}