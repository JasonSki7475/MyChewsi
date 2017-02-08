using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using NLog;
using Provider = ChewsiPlugin.Api.Common.Provider;

namespace ChewsiPlugin.OpenDentalApi
{
    public class OpenDentalApi : DentalApi, IDentalApi
    {
        // TODO Find path to installed application
        private const string OpenDentalInstallationDirectory = @"C:\Program Files (x86)\Open Dental\";
        private const string OpenDentalExeName = @"OpenDental.exe";
        private const string OpenDentalBusinessName = @"OpenDentBusiness.dll";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Dictionary<long, string> _procedureCodes;
        private readonly Proxy _proxy;
        private bool _initialized;

        public OpenDentalApi()
        {
            // Create new app domain and load OpenDental assemblies, then we will load data from them
            AppDomainSetup setup = new AppDomainSetup
            {
                ApplicationBase = OpenDentalInstallationDirectory
            };
            AppDomain domain = AppDomain.CreateDomain("OpenDentalApiDomain", null, setup);
            _proxy = (Proxy) domain.CreateInstanceFromAndUnwrap(typeof (Proxy).Assembly.Location, typeof (Proxy).FullName);
            Logger.Debug("Created proxy class");
            _proxy.InstantiateObject(Path.Combine(OpenDentalInstallationDirectory, OpenDentalExeName), "OpenDental.FormChooseDatabase", null);
            // Set these fields to make it load values from config file
            _proxy.SetField("WebServiceUri", "");
            _proxy.SetField("DatabaseName", "");
            _proxy.InvokeMethod("GetConfig", null);
            //_proxy.InvokeMethod("GetCmdLine", null);
            Logger.Debug("Loaded " + OpenDentalExeName);

            Initialize();
        }

        public PatientInfo GetPatientInfo(string patientId)
        {
            Initialize();
            
            var patient = _proxy.GetPatient(long.Parse(patientId));
            if (patient != null)
            {
                return new PatientInfo
                {
                    BirthDate = patient.Birthdate,
                    FirstName = patient.FName,
                    LastName = patient.LName
                };
            }
            return null;
        }

        private void Initialize()
        {
            if (!_initialized)
            {
                _proxy.Initialize(Path.Combine(OpenDentalInstallationDirectory, OpenDentalBusinessName));
                if ((bool) _proxy.InvokeMethod("TryToConnect", null))
                {
                    Logger.Info("Successfully initialized DB connection. OpenDental version: " + GetVersion());
                    _procedureCodes = _proxy.GetAllCodes().ToDictionary(m => m.CodeNum, m => m.ProcCode);
                    _initialized = true;
                }
                else
                {
                    throw new InvalidOperationException(@"Failed to initialize OpenDental API");
                }
            }
        }

        public List<ProcedureInfo> GetProcedures(string patientId)
        {
            Initialize();
            
            var procedures = _proxy.GetProcedures(long.Parse(patientId));
            if (procedures != null && procedures.Any())
            {
                return procedures.Select(p => new ProcedureInfo
                {
                    Amount = p.ProcFee,
                    Code = _procedureCodes[p.CodeNum],
                    Date = p.ProcDate
                })
                .ToList();
            }
            return null;
        }

        public List<IAppointment> GetAppointmentsForToday()
        {
            Initialize();

            // Find insurance plan by carrier
            //var plan = InsPlans.GetByCarrierName(InsuranceCarrierName);
            var plan = _proxy.GetInsPlanByCarrier(InsuranceCarrierName);

            // Find appointments by insurance plan, dates, status
            var dateRange = GetTimeRangeForToday();
            //var appointments = Appointments.GetAppointmentsStartingWithinPeriod(dateRange.Item1, dateRange.Item2);
            var appointments = _proxy.GetAppointmentsStartingWithinPeriod(dateRange.Item1, dateRange.Item2);
            return new List<IAppointment>(appointments.Where(m => m.AptStatus != "UnschedList" && m.AptStatus != "Broken"
            && (m.InsPlan1 == plan.PlanNum || m.InsPlan2 == plan.PlanNum))
                .Select(m =>
                {
                    var patient = _proxy.GetPatient(m.PatNum);
                    var appointment = new Appointment
                    {
                        Date = m.AptDateTime,
                        InsuranceId = plan.PlanNum.ToString(),
                        IsCompleted = m.AptStatus == "Complete",
                        PatientId = m.PatNum.ToString(),
                        PatientName = $"{patient.FName} {patient.LName}",
                        ProviderId = m.ProvNum.ToString()
                    };
                    return appointment;
                })
                .ToList());
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();

            //var provider = Providers.GetProv(long.Parse(providerId));
            var provider = _proxy.GetProvider(long.Parse(providerId));
            if (provider != null)
            {
                return new Provider
                {
                    // AddressLine = ,
                    // City = ,
                    // Npi = provider.SSN,
                    // State = ,
                    Tin = provider.SSN,
                    // ZipCode = 
                };
            }
            return null;
        }

        public string GetVersion()
        {
            return FileVersionInfo.GetVersionInfo(Path.Combine(OpenDentalInstallationDirectory, OpenDentalBusinessName)).ProductVersion;
        }
    }
}
