using System;
using System.Configuration;
using System.IO;

namespace ChewsiPlugin.Api
{
    /// <summary>
    /// Wraps access to App.config file
    /// </summary>
    public class Settings : ISettings
    {
        private const string SettingsFileName = "App.config";
        private const string SettingsFolderName = "Chewsi"; // The same folder as specified in NLog.config
        private const string DatabaseFileName = "Database.sqlite";
        private static string _settingsFolder;
        private Configuration _config;
        
        public bool Initialized()
        {
            return Directory.Exists(SettingsFolder)
                   && File.Exists(SettingsFileName);
        }

        public void Initialize()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            if (!File.Exists(SettingsFileName))
            {
                // create new file
                LoadSettingsFromFolder(SettingsFolder);
                _config.Save();
            }
        }
        
        public string DatabaseFilePath
        {
            get { return Path.Combine(SettingsFolder, DatabaseFileName); }
        }

        public string SettingsFolder
        {
            get { return _settingsFolder ?? (_settingsFolder = GetSettingsFolder()); }
        }

        private string GetSettingsFolder()
        {
            // c:\ProgramData\Chewsi folder is used for application settings
            var commonSettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), SettingsFolderName);
            ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = Path.Combine(commonSettingsFolder, SettingsFileName)
            };
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            if (config.HasFile)
            {
                // If settings file has InstalledForAllUsers=True, this means that CommonApplicationData is used, otherwise user roaming folder is used ApplicationData
                if (String.Equals(config.AppSettings.Settings["InstalledForAllUsers"].Value, true.ToString(), StringComparison.CurrentCultureIgnoreCase))
                {
                    return commonSettingsFolder;
                }                
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SettingsFolderName);
        }

        private KeyValueConfigurationCollection LoadSettingsFromFolder(string folder)
        {
            ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = Path.Combine(folder, SettingsFileName)
            };
            _config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            return _config.AppSettings.Settings;
        }
        
        public KeyValueConfigurationCollection Get()
        {
            return LoadSettingsFromFolder(SettingsFolder);
        }
    }
}
