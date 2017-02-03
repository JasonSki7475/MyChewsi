using System.Collections.Generic;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApi
    {
        SubscriberInfo GetSubscriberInfo(string patientId);
        ProcedureInfo GetProcedure(string patientId);
        List<IAppointment> GetAppointmentsForToday();
        Provider GetProvider(string providerId);
        string GetVersion();
    }
}
