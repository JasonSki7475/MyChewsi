using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Chewsi
{
    public class RegisterPluginRequest
    {
        private readonly Repository.Settings.PMS.Types _pluginType;

        public RegisterPluginRequest(string tin, string officeAddress1, string officeAddress2, Repository.Settings.PMS.Types pmsType, string pmsVersion)
        {
            TIN = tin;
            Office_Address1 = officeAddress1;
            Office_Address2 = officeAddress2;
            _pluginType = pmsType;
            Practice_Managment_System_Version = $"{pmsType} {pmsVersion}";
        }

        public string TIN { get; private set; }
        public string Office_Address1 { get; private set; }
        public string Office_Address2 { get; private set; }

        public string Plugin_Type { get { return _pluginType.ToString(); } }
        public string Plugin_Version
        {
            get { return Utils.GetPluginVersion(); }
        }
        public string Practice_Managment_System_Version { get; private set; }
        public string Operating_System_Type
        {
            get { return Utils.GetOperatingSystemInfo(); }
        }
    }
}
