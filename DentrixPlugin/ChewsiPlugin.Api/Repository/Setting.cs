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

            public enum Types
            {
                Dentrix,
                OpenDental,
                Patterson
            }
        }
    }
}
