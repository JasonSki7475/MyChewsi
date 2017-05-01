using System;
using System.ServiceProcess;

namespace ChewsiPlugin.Service
{
    static class Program
    {
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                var service = new Service();
                service.Start(new string[] {});
                Console.WriteLine("Press any key to stop service...");
                Console.ReadLine();
                service.Stop();
            }
            else
            {
                var servicesToRun = new ServiceBase[]
                {
                    new Service()
                };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}
