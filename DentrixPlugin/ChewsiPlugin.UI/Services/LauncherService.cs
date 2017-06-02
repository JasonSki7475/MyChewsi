using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using NLog;

namespace ChewsiPlugin.UI.Services
{
    internal class LauncherService : ILauncherService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string ChewsiLauncherRegistryKey = "Chewsi Launcher";
        private const string ChewsiLauncherExecutableName = "ChewsiPlugin.Launcher.exe";

        public bool GetLauncherStartup()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                if (key != null)
                {
                    return key.GetValue(ChewsiLauncherRegistryKey) != null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Cannot load launcher startup settings from registry");
            }
            return false;
        }

        public void SetLauncherStartup(bool enabled)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    if (enabled)
                    {
                        if (key.GetValue(ChewsiLauncherRegistryKey) == null)
                        {
                            key.SetValue(ChewsiLauncherRegistryKey, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ChewsiLauncherExecutableName));
                        }
                    }
                    else
                    {
                        key.DeleteValue(ChewsiLauncherRegistryKey, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Cannot save launcher startup settings in registry");
            }
        }
        
        public void StartLauncher()
        {
            var currentSessionId = Process.GetCurrentProcess().SessionId;
            Process[] runningProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ChewsiLauncherExecutableName));
            if (runningProcesses.All(m => m.SessionId != currentSessionId))
            {
                try
                {
                    Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ChewsiLauncherExecutableName));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to start launcher");
                }
            }
        }

        public void KillLauncher()
        {
            var currentSessionId = Process.GetCurrentProcess().SessionId;
            Process process = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ChewsiLauncherExecutableName)).FirstOrDefault(m => m.SessionId == currentSessionId);
            if (process != null)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to kill launcher");
                }
            }
        }
        
        public void StartPms(string pmsExecutablePath)
        {
            var currentSessionId = Process.GetCurrentProcess().SessionId;
            Process[] runningProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(pmsExecutablePath));
            if (runningProcesses.All(m => m.SessionId != currentSessionId))
            {
                if (File.Exists(pmsExecutablePath))
                {
                    try
                    {
                        Process.Start(pmsExecutablePath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to start PMS");
                    }
                }
                else
                {
                    Logger.Error("Cannot find PMS file: {0}", pmsExecutablePath);
                }
            }
        }
    }
}
