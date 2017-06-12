using System.Runtime.Serialization;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class SettingsDto
    {
        public SettingsDto(Settings.PMS.Types pmsType, string address1, string address2, string tin, bool useProxy, 
            string proxyAddress, int proxyPort, string proxyLogin, string proxyPassword, string state, bool startPms, 
            string machineId, string city, string zip)
        {
            PmsType = pmsType;
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
            MachineId = machineId;
            City = city;
            Zip = zip;
        }

        [DataMember]
        public Settings.PMS.Types PmsType { get; set; }
        
        [DataMember]
        public string Address1 { get; set; }

        [DataMember]
        public string Address2 { get; set; }

        [DataMember]
        public string Tin { get; set; }

        [DataMember]
        public bool UseProxy { get; set; }

        [DataMember]
        public bool StartPms { get; set; }
        
        [DataMember]
        public string ProxyAddress { get; set; }

        [DataMember]
        public int ProxyPort { get; set; }

        [DataMember]
        public string ProxyLogin { get; set; }

        [DataMember]
        public string ProxyPassword { get; set; }

        [DataMember]
        public string State { get; set; }

        [DataMember]
        public string MachineId { get; set; }

        [DataMember]
        public string City { get; set; }

        [DataMember]
        public string Zip { get; set; }
    }
}
