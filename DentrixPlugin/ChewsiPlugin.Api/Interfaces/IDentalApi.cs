using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApi
    {
        PatientInfo GetPatientInfo(string patientId);
        List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate);
        List<Appointment> GetAppointmentsForToday();
        Provider GetProvider(string providerId);
        string GetVersion();
        void Unload();
        string GetPmsExecutablePath(string pmsFolder);
        bool TryGetFolder(out string folder);
        bool IsInitialized();
    }
}
