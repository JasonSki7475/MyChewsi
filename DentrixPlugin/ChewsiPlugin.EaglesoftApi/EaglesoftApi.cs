using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using Dapper;
using Appointment = ChewsiPlugin.Api.Common.Appointment;

namespace ChewsiPlugin.EaglesoftApi
{
    public class EaglesoftApi : DentalApi, IDentalApi
    {
        private readonly IRepository _repository;
        private string _connectionString;
        private const string DatabaseServiceName = "SQLANYs_PattersonDBServer";
        private const int DatabaseServiceStartTimeoutMs = 30000;

        public EaglesoftApi(IRepository repository)
        {
            _repository = repository;
        }

        private OdbcConnection GetConnection()
        {
            var connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public string GetConnectionString()
        {
            string connectionString = null;
            try
            {
                string path;
                if (TryGetFolder(out path))
                {
                    AppDomainSetup setup = new AppDomainSetup
                    {
                        ApplicationBase = path
                    };
                    var domain = AppDomain.CreateDomain("EaglesoftDomain", null, setup);
                    Logger.Info("Loading connection string: app domain created");
                    var obj = domain.CreateInstanceFromAndUnwrap(typeof (Proxy).Assembly.Location, typeof (Proxy).FullName);
                    var proxy = (Proxy) obj;
                    connectionString = proxy.GetConnectionString(path);
                    Logger.Info("Loaded connection string for Eaglesoft");
                    AppDomain.Unload(domain);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Cannot load connection string");
                throw;
            }
            return connectionString;
        }

        private void Initialize()
        {
            if (!_initialized)
            {
                _connectionString = _repository.GetSettingValue<string>(Settings.PMS.ConnectionStringKey);
                _initialized = true;
            }
            if (!TestDatabaseConnection())
            {
                Logger.Info("Starting database server");
                _initialized = StartDatabaseService();
            }            
        }

        private bool TestDatabaseConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    Logger.Debug("Connected to database");
                    return true;
                }
            }
            catch (OdbcException)
            {
                Logger.Warn("Cannot connect to database");
                return false;
            }
        }

        private bool StartDatabaseService()
        {
            try
            {
                var serviceController = new ServiceController(DatabaseServiceName);
                if (serviceController.Status != ServiceControllerStatus.Running)
                {
                    if (serviceController.Status != ServiceControllerStatus.StartPending && serviceController.Status != ServiceControllerStatus.ContinuePending)
                    {
                        serviceController.Start();
                    }
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(DatabaseServiceStartTimeoutMs));
                }
                if (serviceController.Status == ServiceControllerStatus.Running)
                {
                    return true;
                }
            }
            catch (InvalidOperationException e)
            {
                Logger.Debug(e, "Failed to get status and start Eaglesoft database service");
            }
            return false;
        }

        public PatientInfo GetPatientInfo(string patientId)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.QueryFirstOrDefault<PatientInfo>($"SELECT p.prim_member_id as ChewsiId, s.birth_date as BirthDate, p.first_name as PatientFirstName, p.last_name as PatientLastName, s.first_name as SubscriberFirstName, s.last_name as SubscriberLastName FROM patient p JOIN patient s ON p.responsible_party=s.patient_id WHERE p.patient_id={patientId}");
            }
        }

        public List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var list = connection.Query<ProcedureInfo>($@"SELECT date_planned as ""Date"", fee as Amount, service_code as Code FROM planned_services WHERE appt_id={appointmentId}")
                    .ToList();
                list.ForEach(m => m.Code = m.Code.TrimStart(ProcedureCodeFirstCharToTrim));
                return list;
            }
        }

        public override List<Appointment> GetAppointments(DateTime date)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var dateRange = GetTimeRangeForToday(date);

                return new List<Appointment>(connection.Query<Appointment>(
                    $@"SELECT a.appointment_id as Id, a.start_time as ""Date"", a.patient_id as PatientId, p.prim_member_id as ChewsiId, (p.last_name+', '+p.first_name) as PatientName, ap.provider_id as ProviderId, a.date_appointed as PmsModifiedDate
                            FROM appointment a
                            JOIN appointment_provider ap ON ap.appointment_id=a.appointment_id 
                            JOIN patient p ON a.patient_id = p.patient_id 
                            JOIN employer e ON (e.employer_id = p.prim_employer_id OR e.employer_id = p.sec_employer_id)
                            JOIN insurance_company ic ON ic.insurance_company_id = e.insurance_company_id 
                            WHERE a.walkout_time IS NOT NULL
                            AND ic.name = 'Chewsi'
                            AND a.start_time >= '{dateRange.Item1.ToString("O")}' 
                            AND a.start_time <= '{dateRange.Item2.ToString("O")}'"));
            }
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.QueryFirstOrDefault<Provider>($@"SELECT state as State, federal_tax_id as Tin, address_1 as AddressLine1, city as City, address_2 as AddressLine2, national_prov_id as Npi, zipcode as ZipCode FROM provider WHERE provider_id='{providerId}'");
            }
        }

        public string GetVersion()
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.ExecuteScalar<string>("SELECT version FROM system_preferences");
            }
        }
        
        public void Unload()
        {
        }

        public override bool TryGetFolder(out string folder)
        {
            List<string> paths = new List<string>();
            foreach (var drive in DriveInfo.GetDrives().Where(m => m.IsReady && m.DriveType == DriveType.Fixed))
            {
                paths.Add($"{drive}EagleSoft\\Shared Files\\EaglesoftSettings.dll");
            }
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    folder = Path.GetDirectoryName(path);
                    Logger.Info("Found Eaglesoft API in '{0}'", folder);
                    return true;
                }
            }
            Logger.Error("Cannot find Eaglesoft folder");
            folder = null;
            return false;
        }

        public Appointment GetAppointmentById(string id)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return
                    connection.QueryFirstOrDefault<Appointment>(
                        $@"SELECT a.appointment_id as Id, a.start_time as ""Date"", a.patient_id as PatientId, p.prim_member_id as ChewsiId, (p.last_name+', '+p.first_name) as PatientName, ap.provider_id as ProviderId, a.date_appointed as PmsModifiedDate
                            FROM appointment a
                            JOIN appointment_provider ap ON ap.appointment_id=a.appointment_id 
                            JOIN patient p ON a.patient_id = p.patient_id 
                            JOIN employer e ON (e.employer_id = p.prim_employer_id OR e.employer_id = p.sec_employer_id)
                            JOIN insurance_company ic ON ic.insurance_company_id = e.insurance_company_id 
                            WHERE a.appointment_id='{id}'");
            }
        }

        protected override string PmsExeRelativePath => "Eaglesoft.exe";
    }
}
