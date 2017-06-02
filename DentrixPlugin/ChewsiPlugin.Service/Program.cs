using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using ChewsiPlugin.Api.Dentrix;
using NLog;

namespace ChewsiPlugin.Service
{
    static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string DentrixKeyName = "CreateUser_dBNa5Agn.exe";

        static void Main(string [] args)
        {
            var arg = args.FirstOrDefault();
            if (arg == "initDentrix")
            {
                // This method  is called from a custom action during installation
                var api = new DentrixApi();
                if (!api.IsInitialized())
                {
                    Logger.Info("Installing key for Dentrix");
                    // install Dentrix key
                    var process = Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DentrixKeyName));
                    process?.WaitForExit();

                    Logger.Info("Initializing Dentrix API key");
                    // initialize Dentrix API to register user
                    var api2 = new DentrixApi();
                    api2.Unload();
                }
                api.Unload();
            }
#if DEBUG
            else if (Environment.UserInteractive)
            {
                var service = new Service();
                service.Start(new string[] { });
                Console.WriteLine("Press any key to stop service...");
                Console.ReadLine();
                service.Stop();
            }
#endif
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