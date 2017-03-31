using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using Microsoft.Win32;
using NLog;

namespace ChewsiPlugin.Setup.CustomActions
{
    public static class CustomActions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string DentrixKeyName = "CreateUser_dBNa5Agn.exe";
        private const string MainExeFileName = "ChewsiPlugin.UI.exe";
        private const string OpenDentalConfigFileName = "FreeDentalConfig.xml";
        private const string ChewsiLauncherRegistryKey = "Chewsi Launcher";

        public static bool SetCurrentPMS(string pmsType, string installFolder)
        {
            Logger.Info("Started custom action. Type={0}. Folder={1}", pmsType, installFolder);
            var repository = new Repository();
            repository.Initialize();
            var dialogService = new MessageBoxDialogService();
            Settings.PMS.Types pmsTypeSetting = Settings.PMS.Types.Dentrix;
            IDentalApi api = null;

            if (pmsType == "Dentrix")
            {
                pmsTypeSetting = Settings.PMS.Types.Dentrix;
                try
                {
                    Logger.Info("Installing key for Dentrix");
                    // install Dentrix key
                    var process = Process.Start(Path.Combine(installFolder, DentrixKeyName));
                    process?.WaitForExit();

                    Logger.Info("Calling main executable file...");
                    // We can't call Dentrix API from current assembly (?) because Dentrix >= 6.2 binds assembly info to the key
                    // Start main exe file in hidden mode just to register user in Dentrix API
                    // in Dentrix >= 6.2, Dentrix's API user registration window will appear; user has to enter password
                    process = Process.Start(Path.Combine(installFolder, MainExeFileName), "initDentrix");
                    process?.WaitForExit();
                    Logger.Info("...registration completed");
                }
                catch (FileNotFoundException)
                {
                }
                catch (Win32Exception)
                {
                }
                api = new DentrixApi(dialogService);
            }
            else if (pmsType == "OpenDental")
            {
                pmsTypeSetting = Settings.PMS.Types.OpenDental;

                // copy config file into installation folder
                api = new OpenDentalApi.OpenDentalApi(repository, dialogService);
            }
            else if (pmsType == "Eaglesoft")
            {
                pmsTypeSetting = Settings.PMS.Types.Eaglesoft;
                api = new EaglesoftApi.EaglesoftApi(dialogService);
            }

            repository.SaveSetting(Settings.PMS.TypeKey, pmsTypeSetting);
            string folder;
            if (api.TryGetFolder(out folder))
            {
                if (pmsType == "OpenDental")
                {
                    File.Copy(Path.Combine(folder, OpenDentalConfigFileName), installFolder);
                }
            }

            repository.SaveSetting(Settings.PMS.PathKey, folder);
            return true;
        }

        public static void DeleteAutoRunLauncherKeyFromRegistry()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            key?.DeleteValue(ChewsiLauncherRegistryKey, false);
        }
    }
}
