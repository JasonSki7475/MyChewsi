using System;
using System.IO;

namespace ChewsiPlugin.Api
{
    internal static class Utils
    {
        private const string SettingsFolderName = "Chewsi"; // The same folder as specified in NLog.config
        private const string DatabaseFileName = "Database.sqlite";

        public static string DatabaseFilePath
        {
            get { return Path.Combine(SettingsFolder, DatabaseFileName); }
        }

        public static string SettingsFolder
        {
            // TODO use either current user's roaming folder or all-users folder
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SettingsFolderName); }
        }
    }
}
