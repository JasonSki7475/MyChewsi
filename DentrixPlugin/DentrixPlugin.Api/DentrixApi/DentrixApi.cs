using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using NLog;

namespace DentrixPlugin.Api.DentrixApi
{
    public static class DentrixApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly bool _initialized;
        
        static class ApiResult
        {
            public static int Success = 0;
            public static int Fail = 1;
        }

        #region DDP member credentials
        private const string UserId = "dBNa5Agn";
        private const string Password = "XsxJmLGDy";
        private const string KeyFilePath = @"..\dBNa5Agn.dtxkey";
        #endregion

        #region Dll Imports
        private const string DtxApi = "Dentrix.API.dll";

        /// <summary>
        /// >= G6.2
        /// </summary>
        [DllImport(DtxApi, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int DENTRIXAPI_RegisterUser([MarshalAs(UnmanagedType.LPStr)]string szKeyFilePath);

        [DllImport(DtxApi, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern void DENTRIXAPI_GetConnectionString([MarshalAs(UnmanagedType.LPStr)]string szUserId, [MarshalAs(UnmanagedType.LPStr)]string szPassword, StringBuilder szConnectionsString, int ConnectionStringSize); //G5+ available
        
        /// <summary>
        /// >= G5.1
        /// </summary>
        [DllImport(DtxApi, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern float DENTRIXAPI_GetDentrixVersion();
        
        /// <summary>
        /// < G6.2
        /// </summary>
        [DllImport("Dentrix.API.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        static extern int DENTRIXAPI_Initialize(string szUserId, string szPassword);
        #endregion

        public static bool Initialized { get { return _initialized; } }

        public static List<Appointment> GetAppointments()
        {
            string command = "SELECT patid, firstname, lastname FROM admin.patient";
            var connectionString = GetConnectionString();
            if (connectionString.Length > 0)
            {
                Logger.Info("Got connection string");
                using (OdbcConnection conn = new OdbcConnection(connectionString))
                {
                    conn.Open();

                    using (OdbcCommand com = new OdbcCommand(command, conn))
                    {
                        using (OdbcDataReader reader = com.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Logger.Info(reader.GetString(0) + " " + reader.GetString(1) + " " + reader.GetString(2));
                            }
                        }
                    }
                }
            }
            else
            {
                Logger.Error("Could not connect to database.");
            }
            return null;
        }

        static DentrixApi()
        {
            _initialized = Initialize();
        }

        private static bool Initialize()
        {
            var version = DENTRIXAPI_GetDentrixVersion();
            Logger.Info("Dentrix version is " + version);
            bool init = false;
            if ((int) (version*100) >= 1620)
            {
                // G6.2 or higher
                if (DENTRIXAPI_RegisterUser(KeyFilePath) == ApiResult.Success)
                {
                    Logger.Info("Registered user for version >= 6.2");
                    init = true;
                }
                else
                {
                    Logger.Error("Could not register user for version >= 6.2");
                }
            }
            else
            {
                // G6.1 or less
                if (DENTRIXAPI_Initialize(UserId, Password) == 1)
                {
                    Logger.Info("Initialized for version < 6.2");
                    init = true;
                }
                else
                {
                    Logger.Error("Could not register user for version < 6.2");
                }
            }
            return init;
        }

        private static int GetDentrixExePath(StringBuilder retValue)
        {
            int retv = ApiResult.Fail;
            try
            {
                RegistryKey hKey = Registry.CurrentUser.OpenSubKey(@"Software\Dentrix Dental Systems, Inc.\Dentrix\General");
                if (hKey != null)
                {
                    Object value = hKey.GetValue("ExePath");
                    if (value != null)
                    {
                        retValue.Append(value + "Dentrix.API.dll");
                        retv = ApiResult.Success;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Could not read registry value");
            }
            return retv;
        }

        private static string GetConnectionString()
        {
            int connectionStringSize = 512;
            StringBuilder connectionString = new StringBuilder(connectionStringSize);
            string retStr = string.Empty;

            try
            {
                lock (connectionString)
                {
                    DENTRIXAPI_GetConnectionString(UserId, Password, connectionString, connectionStringSize);
                    if (string.IsNullOrWhiteSpace(connectionString.ToString()) == false)
                    {
                        retStr = connectionString.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return retStr;
        }
    }
}
