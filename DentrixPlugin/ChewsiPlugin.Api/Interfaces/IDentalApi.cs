using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApi
    {
        PatientInfo GetPatientInfo(string patientId);
        List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate);
        List<IAppointment> GetAppointmentsForToday();
        Provider GetProvider(string providerId);
        string GetVersion();
        void Unload();
        void Start();
        bool TryGetFolder(out string folder);
        bool Initialized { get; }
    }
}
