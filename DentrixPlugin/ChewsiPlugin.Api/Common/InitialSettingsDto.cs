using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class InitialSettingsDto
    {
        [DataMember]
        public string Tin { get; set; }
        
        [DataMember]
        public string AddressLine1 { get; set; }

        [DataMember]
        public string AddressLine2 { get; set; }

        [DataMember]
        public string State { get; set; }
    }
}
