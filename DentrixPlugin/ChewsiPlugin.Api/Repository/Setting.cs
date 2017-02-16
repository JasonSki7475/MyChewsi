namespace ChewsiPlugin.Api.Repository
{
    public class Setting
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public static class Settings
    {
        public static class PMS
        {
            public const string TypeKey = "PMSType";
            public const string PathKey = "PMSPath";
            public const string VersionKey = "PMSVersion";

            public enum Types
            {
                Dentrix,
                OpenDental,
                Patterson
            }
        }

        public const string MachineIdKey = "MachineId";
        public const string TIN = "TIN";
        public const string Address1Key = "Address1";
        public const string Address2Key = "Address2";
        public const string OsKey = "Os";
        public const string AppVersionKey = "AppVersion";
    }
}
