using System.Runtime.Serialization;
using ChewsiPlugin.Api.Repository;

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

        [DataMember]
        public string City { get; set; }

        [DataMember]
        public string Zip { get; set; }

        [DataMember]
        public Settings.PMS.Types PmsType { get; set; }
        
        [DataMember]
        public bool IsClient { get; set; }
    }
}
