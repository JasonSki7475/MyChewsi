using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class File835Dto
    {
        [DataMember]
        public byte[] Content { get; set; }
    }
}
