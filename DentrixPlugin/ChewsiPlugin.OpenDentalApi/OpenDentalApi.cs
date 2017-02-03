using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.OpenDentalApi
{
    public class OpenDentalApi : IDentalApi
    {
        public SubscriberInfo GetSubscriberInfo(string patientId)
        {
            throw new NotImplementedException();
        }

        public ProcedureInfo GetProcedure(string patientId)
        {
            throw new NotImplementedException();
        }

        public List<IAppointment> GetAppointmentsForToday()
        {
            throw new NotImplementedException();
        }

        public Provider GetProvider(string providerId)
        {
            throw new NotImplementedException();
        }
    }
}
