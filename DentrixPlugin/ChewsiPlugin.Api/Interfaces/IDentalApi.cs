using System.Collections.Generic;
using ChewsiPlugin.Api.Dentrix;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApi
    {
        SubscriberInfo GetSubscriberInfo(string patientId);
        ProcedureInfo GetProcedure(string patientId);
        List<IAppointment> GetAppointmentsForToday();
        Provider GetProvider(string providerId);
    }
}
