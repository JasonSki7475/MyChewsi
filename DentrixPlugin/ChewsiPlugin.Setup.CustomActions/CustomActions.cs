using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Setup.CustomActions
{
    public static class CustomActions
    {
        private const string DentrixKeyName = "CreateUser_dBNa5Agn.exe";
        private const string MainExeFileName = "ChewsiPlugin.UI.exe";
        private const string OpenDentalConfigFileName = "FreeDentalConfig.xml";

        public static bool SetCurrentPMS(string pmsType, string installFolder)
        {
            var repository = new Repository();
            var dialogService = new MessageBoxDialogService();
            if (pmsType == "Dentrix")
            {
                repository.SaveSetting(Settings.PMS.TypeKey, Settings.PMS.Types.Dentrix);
                try
                {
                    // install Dentrix key
                    Process.Start(Path.Combine(installFolder, DentrixKeyName));

                    // start main exe file in hidden mode just to register user in Dentrix API
                    // in Dentrix >= 6.2, Dentrix's API user registration window will appear; user has to enter password
                    Process.Start(Path.Combine(installFolder, MainExeFileName), "initDentrix");
                }
                catch (FileNotFoundException)
                {
                }
                catch (Win32Exception)
                {
                }
            }
            else if (pmsType == "OpenDental")
            {
                // copy config file into installation folder
                var api = new OpenDentalApi.OpenDentalApi(repository, dialogService);
                string folder;
                if (api.TryGetFolder(out folder))
                {
                    File.Copy(Path.Combine(folder, OpenDentalConfigFileName), installFolder);
                    repository.SaveSetting(Settings.PMS.TypeKey, Settings.PMS.Types.OpenDental);
                    repository.SaveSetting(Settings.PMS.PathKey, folder);
                }
            }
            return true;
        }
    }
}
