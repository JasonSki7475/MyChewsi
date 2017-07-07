using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChewsiPlugin.EaglesoftApi
{
    internal class Proxy : MarshalByRefObject
    {
        public string GetConnectionString(string folder, out string message)
        {
            string connectionString = null;
            var sb = new StringBuilder();
            var path = Path.Combine(folder, @"EaglesoftSettings.dll");
            if (File.Exists(path))
            {
                sb.Append("Found EaglesoftSettings. ");
                // LoadFrom loads dependent DLLs (assuming they are in the app domain's base directory
                var assembly = Assembly.LoadFrom(path);

                var type = assembly.GetType("EaglesoftSettings.EncryptedConfiguration");
                var property = type.GetProperty("Client", BindingFlags.Public | BindingFlags.Static);

                var value = property.GetValue(null, null);
                var pairs = (IEnumerable<KeyValuePair<string, string>>)value;
                connectionString = pairs.Where(m => m.Key == "HKLM_Software_Eaglesoft_Assistant_ConnectString").Select(m => m.Value).FirstOrDefault();
                sb.Append("Connection string loaded");
            }
            else
            {
                path = Path.Combine(folder, @"PattersonAppServer.exe");
                if (File.Exists(path))
                {
                    sb.Append("Found PattersonAppServer. ");
                    try
                    {
                        var config = ConfigurationManager.OpenExeConfiguration(path);
                        var settings = ((AppSettingsSection) config.GetSection("appSettings"))?.Settings;
                        if (settings != null)
                        {
                            connectionString = $"DBN={settings["AsstDatabaseName"].Value};DSN={settings["AssistDataSourceName"].Value};UID={settings["AssistDbUserId"].Value};PWD={settings["AssistDbPassword"].Value};";
                            sb.Append("Connection string loaded");
                        }
                        else
                        {
                            sb.Append("Failed to load a section in app settings");
                        }
                    }
                    catch (ConfigurationException e)
                    {
                        sb.Append("Failed to load app settings");
                    }
                    catch (NullReferenceException e)
                    {
                        sb.Append("Failed to load app settings: unsupported format");
                    }
                    /*
                    var assembly = Assembly.LoadFrom(path);
                    var type = assembly.GetType("Patterson.Services.ServiceUtils.Database");
                    var method = type.GetMethod("BuildConnectionString", BindingFlags.Static | BindingFlags.NonPublic);
                    connectionString = method.Invoke(null, null) as string;*/
                }
                else
                {
                    sb.Append("Found none of supported Eaglesoft libraries");
                }
            }
            message = sb.ToString();
            return connectionString;
        }
    }
}
