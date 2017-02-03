using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using Microsoft.Win32;
using NLog;

namespace ChewsiPlugin.Api.Dentrix
{
    public class DentrixApi : IDentalApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool _initialized;
        private const string ChewsiInsuranceCarrierName = "PRINCIPAL";//Chewsi
        
        private static class ApiResult
        {
            public static int Success = 0;
            public static int Fail = 1;
        }

        #region DDP member credentials
        private const string UserId = "dBNa5Agn";
        private const string Password = "XsxJmLGDy";
        private const string KeyFileName = @"dBNa5Agn.dtxkey";
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

        public DentrixApi()
        {
            Initialize();
        }

        /// <summary>
        /// Gets a dictionary with patient-insurance records
        /// </summary>
        private Dictionary<string, string> GetAllPatientsInsurance()
        {
            var result = ExecuteCommand($"select patient_id, primary_insured_id from admin.v_patient_insurance where primary_insurance_carrier_name='{ChewsiInsuranceCarrierName}'",
                new List<string> { "patient_id", "primary_insured_id" }, false);
            return result.ToDictionary(m => m["patient_id"], m => m["primary_insured_id"]);
        }

        public SubscriberInfo GetSubscriberInfo(string patientId)
        {
            Initialize();
            var result =
                ExecuteCommand(
                    $"select pi.first_name, pi.last_name, pi.primary_insured_id, p.birth_date from admin.v_patient_insurance pi join admin.v_patient p on p.patient_id=pi.patient_id where pi.patient_id='{patientId}'",
                    new List<string> {"first_name", "last_name", "primary_insured_id", "birth_date"}, false);
            return result.Select(m => new SubscriberInfo
            {
                //InsuranceId = m["primary_insured_id"],
                LastName = m["last_name"],
                FirstName = m["first_name"],
                BirthDate = DateTime.Parse(m["birth_date"])
            }).FirstOrDefault();
        }

        public ProcedureInfo GetProcedure(string patientId)
        {
            Initialize();
            var dateRange = GetTimeRangeForToday();
            var procedures = Execute("admin.sp_getpatientprocedures",
                new List<string> {"amt", "proc_code", "proc_date"},
                true,
                new Dictionary<string, string>
                {
                    {"patient_guid", patientId},
                    {"BeginDate", dateRange.Item1},
                    {"EndDate", dateRange.Item2},
                    {"byCreateDate", "0"}
                });
            if (procedures != null && procedures.Count != 0)
            {
                var proc = procedures.OrderByDescending(m => DateTime.Parse(m["proc_date"])).First();
                return new ProcedureInfo
                {
                    Amount = proc["amt"],
                    Code = proc["proc_code"],
                    Date = DateTime.Parse(proc["proc_date"])
                };
            }
            return null;
        }

        private Tuple<string, string> GetTimeRangeForToday()
        {
            var now = DateTime.Now;

            /*var dateStart = new DateTime(1993, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(1996, 6, 1, 23, 59, 59);
 */
            var dateStart = new DateTime(2012, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2012, 6, 1, 23, 59, 59);
           
            /*
            var dateStart = now.Date;
            var dateEnd = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);
             */
             return new Tuple<string, string>(dateStart.ToString("G"), dateEnd.ToString("G"));
        }
        
        public List<IAppointment> GetAppointmentsForToday()
        {
            Initialize();
            Dictionary<string, string> patientIds = GetAllPatientsInsurance();
            var dateRange = GetTimeRangeForToday();

            var result = ExecuteCommand($"select patient_id, patient_name, appointment_date, status_id, provider_id from admin.v_appt where appointment_date>'{dateRange.Item1}' and appointment_date<'{dateRange.Item2}' and patient_id in ({string.Join(",", patientIds.Keys).TrimEnd(',')})",
                new List<string> { "patient_id", "patient_name", "appointment_date", "status_id", "provider_id" },
                false,
                new Dictionary<string, string> { { "primary_insurance_carrier_name", ChewsiInsuranceCarrierName } });
            
            return new List<IAppointment>(result.Select(m =>
            {
                string insuranceId;
                patientIds.TryGetValue(m["patient_id"], out insuranceId);
                return new Appointment
                {
                    PatientName = m["patient_name"],
                    PatientId = m["patient_id"],
                    Date = DateTime.Parse(m["appointment_date"]),
                    StatusId = m["status_id"],
                    ProviderId = m["provider_id"],
                    InsuranceId = insuranceId
                };
            }).ToList());
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();
            var result = ExecuteCommand($"select tin, npi, address_line1, state, city, zip_code from admin.v_provider where provider_id=\'{providerId}\'",
                new List<string> { "tin", "npi", "address_line1", "state", "city", "zip_code" }, false);

            return result.Select(m => new Provider
            {
                AddressLine = m["address_line1"],
                City = m["city"],
                Npi = m["npi"],
                State = m["state"],
                Tin = m["tin"],
                ZipCode = m["zip_code"]
            }).FirstOrDefault();
        }

        public string GetVersion()
        {
            return DENTRIXAPI_GetDentrixVersion().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets data from a view or a stored procedure
        /// </summary>
        /// <param name="name">Name of the view or the stored procedure</param>
        private List<Dictionary<string, string>> Execute(string name, List<string> outputFields, bool isStoredProcedure = false, Dictionary<string, string> parameters = null)
        {
            string commandText;
            if (isStoredProcedure)
            {
                commandText = "{call " + name + "(" + (parameters == null
                    ? ""
                    : string.Join("", parameters.Keys.Select(m => "?,")).TrimEnd(',')) + ")}";
            }
            else
            {
                commandText = "select * from " + name;
                if (parameters != null)
                {
                    commandText += " where ";
                    foreach (var parameter in parameters)
                    {
                        commandText += parameter.Key + "=\'" + parameter.Value + "\' and ";
                    }
                    commandText = commandText.Remove(commandText.Length - 5, 4);
                }
            }

            return ExecuteCommand(commandText, outputFields, isStoredProcedure, parameters);
        }

        private List<Dictionary<string, string>> ExecuteCommand(string commandText, List<string> outputFields, bool isStoredProcedure, Dictionary<string, string> parameters = null)
        {
            var result = new List<Dictionary<string, string>>();
            var connectionString = GetConnectionString();
            if (connectionString.Length > 0)
            {
                Logger.Info("Got connection string");
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    connection.Open();

                    using (OdbcCommand command = new OdbcCommand(commandText, connection))
                    {
                        if (isStoredProcedure)
                        {
                            if (parameters != null)
                            {
                                foreach (var parameter in parameters)
                                {
                                    command.Parameters.AddWithValue("@" + parameter.Key, parameter.Value);
                                }
                            }
                            command.CommandType = CommandType.StoredProcedure;
                            Logger.Debug("Executing stored procedure: " + commandText);
                        }
                        else
                        {
                            Logger.Debug("Executing query: " + commandText);
                        }

                        try
                        {
                            using (OdbcDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var r = new Dictionary<string, string>();
                                    foreach (var field in outputFields)
                                    {
                                        r[field] = reader[field].ToString();
                                    }
                                    result.Add(r);
                                }
                            }
                        }
                        catch (OdbcException objEx)
                        {
                            Logger.Error("Failed to execute " + (isStoredProcedure ? "stored procedure: " : "query: ") + objEx.Message);
                        }
                    }
                }
            }
            else
            {
                Logger.Error("Could not connect to database.");
            }
            return result;
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            var version = DENTRIXAPI_GetDentrixVersion();
            Logger.Info("Dentrix version is " + version);
            if ((int) (version*100) >= 1620)
            {
                // G6.2 or higher
                var keyFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), KeyFileName);
                if (DENTRIXAPI_RegisterUser(keyFilePath) == ApiResult.Success)
                {
                    Logger.Info("Registered user for version >= 6.2");
                    _initialized = true;
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
                    _initialized = true;
                }
                else
                {
                    Logger.Error("Could not register user for version < 6.2");
                }
            }
        }

        private int GetDentrixExePath(StringBuilder retValue)
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

        private string GetConnectionString()
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
