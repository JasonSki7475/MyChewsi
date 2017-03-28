using System.Collections.Generic;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApi
    {
        PatientInfo GetPatientInfo(string patientId);
        List<ProcedureInfo> GetProcedures(string patientId, string appointmentId);
        List<IAppointment> GetAppointmentsForToday();
        Provider GetProvider(string providerId);
        string GetVersion();
        string Name { get; }
        void Unload();
        //bool Initialized { get; }
        void Start();
        bool TryGetFolder(out string folder);
    }
}
