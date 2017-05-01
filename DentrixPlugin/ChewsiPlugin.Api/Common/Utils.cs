using System;
using System.Reflection;

namespace ChewsiPlugin.Api.Common
{
    public static class Utils
    {
        public static string GetPluginVersion()
        {
            return Assembly.GetCallingAssembly().GetName().Version.ToString();
        }

        public static string GetOperatingSystemInfo()
        {
            return Environment.OSVersion.ToString();
        }
    }
}
