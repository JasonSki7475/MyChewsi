using System;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class DownloadDto
    {
        [DataMember]
        public string Edi { get; set; }

        [DataMember]
        public string Report { get; set; }

        [DataMember]
        public string Status { get; set; }

        [DataMember]
        public string PostedDate { get; set; }
    }
}
