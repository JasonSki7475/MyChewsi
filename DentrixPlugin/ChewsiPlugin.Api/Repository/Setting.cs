﻿namespace ChewsiPlugin.Api.Repository
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
            public const string ConnectionStringKey = "PMSConnectionString";

            public enum Types
            {
                Dentrix,
                OpenDental,
                Eaglesoft
            }
        }

        public const string MachineIdKey = "MachineId";
        public const string TIN = "TIN";
        public const string Address1Key = "Address1";
        public const string Address2Key = "Address2";
        public const string StateKey = "State";
        public const string OsKey = "Os";
        public const string AppVersionKey = "AppVersion";
        public const string UseProxy = "UseProxy";
        public const string StartPms = "StartPms";
        public const string ProxyAddress = "ProxyAddress";
        public const string ProxyPort = "ProxyPort";
        public const string ProxyLogin = "ProxyLogin";
        public const string ProxyPassword = "ProxyPassword";
        public const string IsClient = "IsClient";
        public const string ServerAddress = "ServerAddress";
        public const string City = "City";
        public const string Zip = "Zip";
    }
}
