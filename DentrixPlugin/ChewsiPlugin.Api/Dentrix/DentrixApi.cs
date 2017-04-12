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

namespace ChewsiPlugin.Api.Dentrix
{
    public class DentrixApi : DentalApi, IDentalApi
    {      
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

        public DentrixApi(IDialogService dialogService)
        {
            _dialogService = dialogService;
            Initialize();
        }

        /// <summary>
        /// Gets a dictionary with patient-insurance records
        /// </summary>
        private Dictionary<string, string> GetAllPatientsInsurance()
        {
            var result = ExecuteCommand($"select pi.patient_id, ins.id_num from admin.v_patient_insurance pi join admin.v_insured ins on pi.primary_insured_id=ins.insured_id where primary_insurance_carrier_name='{InsuranceCarrierName}' or secondary_insurance_carrier_name='{InsuranceCarrierName}'",
                new List<string> { "patient_id", "id_num" }, false);
            return result.ToDictionary(m => m["patient_id"].Trim(), m => m["id_num"].Trim());
        }

        public PatientInfo GetPatientInfo(string patientId)
        {
            var result =
                ExecuteCommand(
                    $"select pi.primary_insured_first_name, pi.primary_insured_last_name, pi.first_name, pi.last_name, ins.id_num, p.birth_date from admin.v_patient_insurance pi join admin.v_patient p on p.patient_id=pi.patient_id join admin.v_insured ins on pi.primary_insured_id=ins.insured_id where pi.patient_id='{patientId}'",
                    new List<string> {"first_name", "last_name", "id_num", "birth_date", "primary_insured_first_name", "primary_insured_last_name" }, false);
            return result.Select(m =>
            {
                var pi = new PatientInfo
                {
                    ChewsiId = m["id_num"].Trim(),
                    PatientLastName = m["last_name"].Trim(),
                    PatientFirstName = m["first_name"].Trim(),
                    SubscriberFirstName = m["primary_insured_first_name"].Trim(),
                    SubscriberLastName = m["primary_insured_last_name"].Trim(),
                    BirthDate = null
                };
                DateTime birthDate;
                if (DateTime.TryParse(m["birth_date"], out birthDate))
                {
                    pi.BirthDate = birthDate;
                }
                return pi;
            }).FirstOrDefault();
        }

        public List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate)
        {
            var beginDate = appointmentDate.Date;
            var endDate = appointmentDate.Date.AddDays(1).AddSeconds(-1);

            // get patient_guid
            var res =
                ExecuteCommand($"select pi.patient_guid from admin.v_patient_insurance pi where pi.patient_id='{patientId}'",
                    new List<string> { "patient_guid" }, false).FirstOrDefault();
            if (res != null)
            {
                var patientGuid = res["patient_guid"];
                var procedures = Execute("admin.sp_getpatientprocedures",
                    new List<string> {"amt", "proc_code", "proc_date"},
                    true,
                    new Dictionary<string, string>
                    {
                        {"patient_guid", patientGuid},
                        {"BeginDate", beginDate.ToString("G")},
                        {"EndDate", endDate.ToString("G")},
                        {"byCreateDate", "0"}
                    });
                if (procedures != null && procedures.Count != 0)
                {
                    return procedures.Select(proc => 
                    new ProcedureInfo
                    {
                        Amount = double.Parse(proc["amt"]),
                        Code = proc["proc_code"].TrimStart('D'),
                        Date = DateTime.Parse(proc["proc_date"])
                    })
                    .ToList();
                }
            }
            return new List<ProcedureInfo>();
        }
       
        public List<IAppointment> GetAppointmentsForToday()
        {
            Dictionary<string, string> patientIds = GetAllPatientsInsurance();
            if (patientIds.Count == 0)
            {
                Logger.Warn("No Chewsi patients found");
                return new List<IAppointment>();
            }

            var dateRange = GetTimeRangeForToday();

            var result = ExecuteCommand($"select appointment_id, patient_id, patient_name, appointment_date, provider_id from admin.v_appt where (status_id='150' or status_id='-106') and appointment_date>'{dateRange.Item1}' and appointment_date<'{dateRange.Item2}'" +
                (patientIds.Any() ? $" and patient_id in ({string.Join(", ", patientIds.Keys).TrimEnd(',')})":""),
                new List<string> { "patient_id", "patient_name", "appointment_date", "provider_id", "appointment_id" },
                false);
            
            return new List<IAppointment>(result.Select(m =>
            {
                string insuranceId;
                patientIds.TryGetValue(m["patient_id"], out insuranceId);
                return new Appointment
                {
                    Id = m["appointment_id"].Trim(),
                    PatientName = m["patient_name"].Trim(),
                    PatientId = m["patient_id"].Trim(),
                    Date = DateTime.Parse(m["appointment_date"]),
                    ProviderId = m["provider_id"].Trim(),
                    ChewsiId = insuranceId
                };
            }).ToList());
        }

        public Provider GetProvider(string providerId)
        {
            var result = ExecuteCommand($"select tin, npi, address_line1, address_line2, state, city, zip_code from admin.v_provider where provider_id=\'{providerId}\'",
                new List<string> { "tin", "npi", "address_line1", "address_line2", "state", "city", "zip_code" }, false);

            return result.Select(m => new Provider
            {
                AddressLine1 = m["address_line1"].Trim(),
                AddressLine2 = m["address_line2"].Trim(),
                City = m["city"].Trim(),
                Npi = m["npi"].Trim(),
                State = m["state"].Trim(),
                Tin = m["tin"].Trim(),
                ZipCode = m["zip_code"].Trim()
            }).FirstOrDefault();
        }

        public string GetVersion()
        {
            if (_initialized)
            {
                return DENTRIXAPI_GetDentrixVersion().ToString(CultureInfo.InvariantCulture);
            }
            return null;
        }

        public override bool TryGetFolder(out string folder)
        {
            return GetDentrixFolder(out folder);
        }
        
        protected override string PmsExeRelativePath => "Office.exe";

        public void Unload()
        {
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

        private List<Dictionary<string, string>> ExecuteCommand(string commandText, List<string> outputFields, bool isStoredProcedure, Dictionary<string, string> storedProcedureParameters = null)
        {
            var result = new List<Dictionary<string, string>>();
            if (!_initialized)
            {
                return result;
            }

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
                            string param = "";
                            if (storedProcedureParameters != null)
                            {
                                foreach (var parameter in storedProcedureParameters)
                                {
                                    command.Parameters.AddWithValue("@" + parameter.Key, parameter.Value);
                                    param += $"{parameter.Key}={parameter.Value},";
                                }
                            }
                            command.CommandType = CommandType.StoredProcedure;
                            
                            Logger.Debug($"Executing stored procedure: {commandText}; parameters: {param.TrimEnd(',')}");
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

            try
            {
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
                    try
                    {
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
                    catch (UnauthorizedAccessException e)
                    {
                        Logger.Error(e, "Failed to initialize Dentrix API");
                        _dialogService.Show($"Failed to initialize Dentrix API: {e.Message}. Try to run the plugin as administrator.");
                    }
                }
            }
            catch (DllNotFoundException)
            {
                _dialogService.Show("Unable to find Dentrix API library. Make sure Dentrix G is installed.", "Error");
            }
        }

        private bool GetDentrixFolder(out string path)
        {
            var result = false;
            path = null;
            try
            {
                RegistryKey hKey = Registry.CurrentUser.OpenSubKey(@"Software\Dentrix Dental Systems, Inc.\Dentrix\General");
                if (hKey != null)
                {
                    var value = hKey.GetValue("ExePath");
                    if (value != null)
                    {
                        path = value.ToString();
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not load Dentrix folder from registry");
            }
            return result;
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
