using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.ServiceProcess;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using Dapper;

namespace ChewsiPlugin.EaglesoftApi
{
    public class EaglesoftApi : DentalApi, IDentalApi
    {
        private string _connectionString;
        private const string DatabaseServiceName = "SQLANYs_PattersonDBServer";
        private const int DatabaseServiceStartTimeoutMs = 30000;

        public EaglesoftApi()
        {
        }

        private OdbcConnection GetConnection()
        {
            var connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void Initialize()
        {
            if (!_initialized)
            {
                AppDomainSetup setup = new AppDomainSetup
                {
                    ApplicationBase = @"C:\EagleSoft\Shared Files\"
                };
                var domain = AppDomain.CreateDomain("EaglesoftDomain", null, setup);
                var proxy = (Proxy)domain.CreateInstanceFromAndUnwrap(typeof(Proxy).Assembly.Location, typeof(Proxy).FullName);
                _connectionString = proxy.GetConnectionString();
                AppDomain.Unload(domain);
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
                return connection.QueryFirstOrDefault<PatientInfo>($"SELECT p.prim_member_id as ChewsiId, p.birth_date as BirthDate, p.first_name as PatientFirstName, p.last_name as PatientLastName, s.first_name as SubscriberFirstName, s.last_name as SubscriberLastName FROM patient p JOIN patient s ON p.responsible_party=s.patient_id WHERE p.patient_id={patientId}");
            }
        }

        public List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.Query<ProcedureInfo>($@"SELECT date_planned as Date, fee as Amount, service_code as Code FROM planned_services WHERE appt_id={appointmentId}").ToList();
            }
        }

        public List<Appointment> GetAppointmentsForToday()
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var dateRange = GetTimeRangeForToday();

                return new List<Appointment>(connection.Query<Appointment>(
                        $@"SELECT a.appointment_id as Id, a.start_time as 'Date', a.patient_id as PatientId, p.prim_member_id as ChewsiId, (p.last_name+', '+p.first_name) as PatientName, ap.provider_id as ProviderId 
                            FROM appointment a, patient p, insurance_company ic, employer e, appointment_provider ap
                            WHERE a.patient_id = p.patient_id AND ic.insurance_company_id = e.insurance_company_id AND (ic.name = 'Chewsi')
                            AND ap.appointment_id=a.appointment_id AND (e.employer_id = p.prim_employer_id OR e.employer_id = p.sec_employer_id)
                            AND a.start_time > '{dateRange.Item1.ToString("O")}' AND a.start_time < '{dateRange.Item2.ToString("O")}'"));
            }
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.QueryFirstOrDefault<Provider>(@"SELECT state as State, federal_tax_id as Tin, address_1 as AddressLine1, city as City, address_2 as AddressLine2, national_provider_id as Npi, zipcode as ZipCode FROM practice");
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
            folder = @"C:\EagleSoft\Shared Files\";
            return true;
        }
        
        protected override string PmsExeRelativePath => "Eaglesoft.exe";
    }
}
