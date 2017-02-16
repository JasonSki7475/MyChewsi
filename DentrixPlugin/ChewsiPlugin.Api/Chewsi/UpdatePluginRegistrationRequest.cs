namespace ChewsiPlugin.Api.Chewsi
{
    public class UpdatePluginRegistrationRequest
    {
        public UpdatePluginRegistrationRequest(string machineId, string officeAddress1, string officeAddress2, Repository.Settings.PMS.Types pmsType, string pmsVersion)
        {
            Token = machineId;
            Office_Address1 = officeAddress1;
            Office_Address2 = officeAddress2;
            Practice_Managment_System_Version = $"{pmsType} {pmsVersion}";
        }

        public string Token { get; private set; }        
        public string Office_Address1 { get; private set; }
        public string Office_Address2 { get; private set; }
        
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
