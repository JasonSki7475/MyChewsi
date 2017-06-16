using System;
using System.Collections.Generic;

namespace ChewsiPlugin.Api.Repository
{
    public interface IRepository
    {
        bool Ready { get; }
        void Initialize();

        void AddAppointment(Appointment item);
        List<Appointment> GetAppointments();
        Appointment GetAppointmentById(string id);
        void UpdateAppointment(Appointment item);
        void BulkDeleteAppointments(List<string> ids);

        T GetSettingValue<T>(string key);
        void SaveSetting(string key, object value);
        

        IEnumerable<SubmittedProcedure> GetSubmittedProcedures(string patientId, string providerId, DateTime date);
        void AddSubmittedProcedures(IEnumerable<SubmittedProcedure> procedures);
        void BulkDeleteSubmittedProcedures(List<int> ids);
        List<SubmittedProcedure> GetSubmittedProcedures();

        bool DeletedStatusExists(string providerId, string chewsiId, DateTime date);
        void BulkDeleteDeletedStatuses(List<string> ids);
        List<DeletedStatus> GetDeletedStatuses();
        void AddDeletedStatus(string providerId, string chewsiId, DateTime date);
    }
}