using System.Runtime.Serialization;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class SettingsDto
    {
        public SettingsDto(Settings.PMS.Types pmsType, string pmsPath, string address1, string address2, string tin, bool useProxy, 
            string proxyAddress, int proxyPort, string proxyLogin, string proxyPassword, string state, bool startPms, 
            bool startLauncher, string machineId, bool isClient)
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
            MachineId = machineId;
            IsClient = isClient;
        }

        [DataMember]
        public Settings.PMS.Types PmsType { get; }

        [DataMember]
        public string PmsPath { get; }

        [DataMember]
        public string Address1 { get; }

        [DataMember]
        public string Address2 { get; }

        [DataMember]
        public string Tin { get; }

        [DataMember]
        public bool UseProxy { get; }

        [DataMember]
        public bool StartPms { get; }

        [DataMember]
        public bool StartLauncher { get; }

        [DataMember]
        public string ProxyAddress { get; }

        [DataMember]
        public int ProxyPort { get; }

        [DataMember]
        public string ProxyLogin { get; }

        [DataMember]
        public string ProxyPassword { get; }

        [DataMember]
        public string State { get; }

        [DataMember]
        public string MachineId { get; }

        [DataMember]
        public bool IsClient { get; }
    }
}
