using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI.Models
{
    internal class SettingsDto
    {
        public SettingsDto(Settings.PMS.Types pmsType, string pmsPath, string address1, string address2, string tin, 
            bool useProxy, string proxyAddress, int proxyPort, string proxyLogin, string proxyPassword, string state, bool startPms, bool startLauncher)
        {
            PmsType = pmsType;
            PmsPath = pmsPath;
            Address1 = address1;
            Address2 = address2;
            Tin = tin;
            UseProxy = useProxy;
            ProxyAddress = proxyAddress;
            ProxyPort = proxyPort;
            ProxyLogin = proxyLogin;
            ProxyPassword = proxyPassword;
            State = state;
            StartPms = startPms;
            StartLauncher = startLauncher;
        }

        public Settings.PMS.Types PmsType { get; }

        public string PmsPath { get; }

        public string Address1 { get; }

        public string Address2 { get; }

        public string Tin { get; }

        public bool UseProxy { get; }

        public bool StartPms { get; }

        public bool StartLauncher { get; }

        public string ProxyAddress { get; }

        public int ProxyPort { get; }

        public string ProxyLogin { get; }

        public string ProxyPassword { get; }

        public string State { get; }
    }
}
