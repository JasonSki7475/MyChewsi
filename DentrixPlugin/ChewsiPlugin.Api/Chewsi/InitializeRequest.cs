using System;
using System.Reflection;

namespace ChewsiPlugin.Api.Chewsi
{
    public class InitializeRequest
    {
        private readonly PluginType _pluginType;

        public InitializeRequest(PluginType pluginType, string practiceManagementSystemVersion, ProviderInformationRequest providerInformation)
        {
            _pluginType = pluginType;
            PracticeManagementSystemVersion = practiceManagementSystemVersion;
            TIN = providerInformation.TIN;
            AddressLine1 = providerInformation.RenderingAddress;
            State = providerInformation.RenderingState;
            City = providerInformation.RenderingCity;
            Zip = providerInformation.RenderingZip;
        }

        public string TIN { get; set; }
        public string AddressLine1 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string Zip { get; set; }

        public string PluginType { get { return _pluginType.ToString(); } }

        public string PluginVersion
        {
            get { return Assembly.GetCallingAssembly().GetName().Version.ToString(); }
        }
        public string PracticeManagementSystemVersion { get; private set; }
        public string OperatingSystemVersion
        {
            get { return Environment.OSVersion.ToString(); }
        }
    }
}
