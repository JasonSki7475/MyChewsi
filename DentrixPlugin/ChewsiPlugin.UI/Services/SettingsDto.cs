using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI.Services
{
    public class SettingsDto
    {
        private Settings.PMS.Types _pmsType;
        private string _pmsPath;
        private string _address1;
        private string _address2;
        private string _tin;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyLogin;
        private string _proxyPassword;
        private string _state;
        private bool _startPms;

        public SettingsDto(Settings.PMS.Types pmsType, string pmsPath, string address1, string address2, string tin, 
            bool useProxy, string proxyAddress, int proxyPort, string proxyLogin, string proxyPassword, string state, bool startPms)
        {
            _pmsType = pmsType;
            _pmsPath = pmsPath;
            _address1 = address1;
            _address2 = address2;
            _tin = tin;
            _useProxy = useProxy;
            _proxyAddress = proxyAddress;
            _proxyPort = proxyPort;
            _proxyLogin = proxyLogin;
            _proxyPassword = proxyPassword;
            _state = state;
            _startPms = startPms;
        }

        public Settings.PMS.Types PmsType
        {
            get { return _pmsType; }
        }

        public string PmsPath
        {
            get { return _pmsPath; }
        }

        public string Address1
        {
            get { return _address1; }
        }

        public string Address2
        {
            get { return _address2; }
        }

        public string Tin
        {
            get { return _tin; }
        }

        public bool UseProxy
        {
            get { return _useProxy; }
        }

        public bool StartPms
        {
            get { return _startPms; }
        }

        public string ProxyAddress
        {
            get { return _proxyAddress; }
        }

        public int ProxyPort
        {
            get { return _proxyPort; }
        }

        public string ProxyLogin
        {
            get { return _proxyLogin; }
        }

        public string ProxyPassword
        {
            get { return _proxyPassword; }
        }

        public string State
        {
            get { return _state; }
        }
    }
}
