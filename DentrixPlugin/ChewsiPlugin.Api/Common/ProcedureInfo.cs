using System;
using System.Runtime.Serialization;

namespace ChewsiPlugin.Api.Common
{
    [DataContract]
    public class ProcedureInfo
    {
        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public double Amount { get; set; }
    }
}
