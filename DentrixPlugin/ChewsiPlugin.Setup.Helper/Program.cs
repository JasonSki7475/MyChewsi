using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ChewsiPlugin.Setup.Helper
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string MainExeFileName = "ChewsiPlugin.Service.exe";
        private const string OpenDentalConfigFileName = "FreeDentalConfig.xml";

        static void Main(string[] args)
        {
            try
            {
                LogManager.Configuration = new LoggingConfiguration();
                ConfigurationItemFactory.Default.Targets.RegisterDefinition("FileTarget", typeof(FileTarget));
                var target = new FileTarget("FileTarget")
                {
                    FileName = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), $"ChewsiInstallationLog-{DateTime.Now.ToString("s").Replace(":", "-")}.txt")
                };
                LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, target));
                LogManager.ReconfigExistingLoggers();

                var repository = new Repository();
                repository.Initialize();
                var pmsType = repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            
                Logger.Info("PMS type is '{0}'. Initializing dental API", pmsType);

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
                        Logger.Info("Getting connection string");
                        api = new OpenDentalApi.OpenDentalApi2(repository);
                        var cs = ((OpenDentalApi.OpenDentalApi2)api).GetConnectionString();
                        repository.SaveSetting(Settings.PMS.ConnectionStringKey, cs);
                        break;
                    case Settings.PMS.Types.Eaglesoft:
                        Logger.Info("Getting connection string");
                        api = new EaglesoftApi.EaglesoftApi(repository);
                        cs = ((EaglesoftApi.EaglesoftApi)api).GetConnectionString();
                        repository.SaveSetting(Settings.PMS.ConnectionStringKey, cs);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                string folder;
                if (api.TryGetFolder(out folder))
                {
                    // Copy config file into installation folder
                    // Disabled for OpenDentalApi2
                    //if (pmsType == Settings.PMS.Types.OpenDental)
                    //{
                    //    File.Copy(Path.Combine(folder, OpenDentalConfigFileName), Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), OpenDentalConfigFileName), true);
                    //    Logger.Info("Copied OpenDental configuration file");
                    //}
                }
                repository.SaveSetting(Settings.PMS.PathKey, folder);

                Logger.Info("Setup helper: dental API initialization completed");
            }
            catch(Exception e)
            {
                Logger.Error(e, "Setup helper: error initializing dental API");
                throw;
            }
        }
    }
}
