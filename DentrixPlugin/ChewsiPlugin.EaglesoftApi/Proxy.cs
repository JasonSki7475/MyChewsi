using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ChewsiPlugin.EaglesoftApi
{
    internal class Proxy : MarshalByRefObject
    {
        public string GetConnectionString(string folder)
        {
            // LoadFrom loads dependent DLLs (assuming they are in the app domain's base directory
            var assembly = Assembly.LoadFrom(Path.Combine(folder, @"EaglesoftSettings.dll"));

            Type type = assembly.GetType("EaglesoftSettings.EncryptedConfiguration");
            var property = type.GetProperty("Client", BindingFlags.Public | BindingFlags.Static);

            var value = property.GetValue(null, null);
            var pairs = (IEnumerable<KeyValuePair<string, string>>)value;
            return pairs.Where(m => m.Key == "HKLM_Software_Eaglesoft_Assistant_ConnectString").Select(m => m.Value).FirstOrDefault();
        }
    }
}
