using System;
using System.ServiceModel;
using Patterson.Services.ScheduleService;

namespace ChewsiPlugin.Eaglesoft.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var myBinding = new BasicHttpBinding();
            var myEndpoint = new EndpointAddress("net.tcp://localhost:2010/AppointmentService");
            var myChannelFactory = new DuplexChannelFactory<IAppointmentDataService>(myBinding, myEndpoint);

            IAppointmentDataService client = null;

            try
            {
                client = myChannelFactory.CreateChannel();
                var a = client.GetNextAppointmentDate("", 1);//.GetEClaimCount();
                ((ICommunicationObject)client).Close();
            }
            catch(Exception e)
            {
                if (client != null)
                {
                    ((ICommunicationObject)client).Abort();
                }
            }
        }
    }
}
