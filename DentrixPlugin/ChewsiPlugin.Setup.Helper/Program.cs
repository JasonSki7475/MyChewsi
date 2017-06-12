using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using NLog;

namespace ChewsiPlugin.Setup.Helper
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string MainExeFileName = "ChewsiPlugin.Service.exe";
        private const string OpenDentalConfigFileName = "FreeDentalConfig.xml";

        static void Main(string[] args)
        {
            var repository = new Repository();
            repository.Initialize();
            var pmsType = repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            
            IDentalApi api;
            switch (pmsType)
            {
                case Settings.PMS.Types.Dentrix:
                    try
                    {
                        Logger.Info("Calling main executable file...");
                        // We can't call Dentrix API from current assembly (?) because Dentrix >= 6.2 binds assembly info to the key
                        // Start main exe file in hidden mode just to register user in Dentrix API
                        // in Dentrix >= 6.2, Dentrix's API user registration window will appear; user has to enter password
                        var process = Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), MainExeFileName), "initDentrix");
                        process?.WaitForExit();
                        Logger.Info("...registration completed");
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    catch (Win32Exception)
                    {
                    }
                    api = new DentrixApi();
                    break;
                case Settings.PMS.Types.OpenDental:
                    // copy config file into installation folder
                    api = new OpenDentalApi.OpenDentalApi(repository);
                    break;
                case Settings.PMS.Types.Eaglesoft:
                    api = new EaglesoftApi.EaglesoftApi(repository);
                    var cs = ((EaglesoftApi.EaglesoftApi)api).GetConnectionString();
                    repository.SaveSetting(Settings.PMS.ConnectionStringKey, cs);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            string folder;
            if (api.TryGetFolder(out folder))
            {
                if (pmsType == Settings.PMS.Types.OpenDental)
                {
                    File.Copy(Path.Combine(folder, OpenDentalConfigFileName), Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), OpenDentalConfigFileName), true);
                }
            }
            repository.SaveSetting(Settings.PMS.PathKey, folder);
        }
    }
}
