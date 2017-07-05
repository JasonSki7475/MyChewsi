using System;
using ChewsiPlugin.Api.Repository;
using Microsoft.Win32;
using NLog;
using NLog.Config;

namespace ChewsiPlugin.Setup.CustomActions
{
    public static class CustomActions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string ChewsiLauncherRegistryKey = "Chewsi Launcher";

        const uint ERROR_INSTALL_FAILURE = 1603;
        const uint ERROR_SUCCESS = 0;

        public static uint Setup(string pmsType, string installDir, string isClient, string msiHandle)
        {
            if (!string.IsNullOrEmpty(msiHandle))
            {
                ConfigurationItemFactory.Default.Targets.RegisterDefinition("MsiTarget", typeof (MsiTarget));
                LogManager.Configuration = new LoggingConfiguration();
                LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, new MsiTarget(int.Parse(msiHandle))));
                LogManager.ReconfigExistingLoggers();
            }

            uint result;
            try
            {
                Logger.Info("Setting current PMS. Type={0}. Folder={1}", pmsType, installDir);
                var repository = new Repository();
                repository.Initialize();
                Settings.PMS.Types pmsTypeSetting;

                switch (pmsType)
                {
                    case "Dentrix":
                        pmsTypeSetting = Settings.PMS.Types.Dentrix;
                        break;
                    case "OpenDental":
                        pmsTypeSetting = Settings.PMS.Types.OpenDental;
                        break;
                    case "Eaglesoft":
                        pmsTypeSetting = Settings.PMS.Types.Eaglesoft;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                repository.SaveSetting(Settings.PMS.TypeKey, pmsTypeSetting);
                
                Logger.Info("Setting IsClient flag. Value={0}", isClient);
                repository.SaveSetting(Settings.IsClient, isClient == "True");
                result = ERROR_SUCCESS;
            }
            catch (Exception ex)
            {
                result = ERROR_INSTALL_FAILURE;
                Logger.Error(ex, "Failed to initialize settings for " + pmsType);
            }
            return result;
        }

        public static void DeleteAutoRunLauncherKeyFromRegistry()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            key?.DeleteValue(ChewsiLauncherRegistryKey, false);
        }
    }
}
