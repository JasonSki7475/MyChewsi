using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using NLog;
using OpenDentBusiness;
using Provider = ChewsiPlugin.Api.Common.Provider;

namespace ChewsiPlugin.OpenDentalApi
{
    public class OpenDentalApi : DentalApi, IDentalApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _path;
        private readonly Dictionary<long, string> _procedureCodes;
        private readonly Proxy _proxy;
        private readonly bool _initialized;

        public OpenDentalApi()
        {
            // TODO Find path to installed application
            _path = @"C:\Program Files (x86)\Open Dental\OpenDentBusiness.dll";
            var exePath = @"C:\Program Files (x86)\Open Dental\OpenDental.exe";
            
            AppDomainSetup setup = new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(_path)
            };
            AppDomain domain = AppDomain.CreateDomain("OpenDentalApiDomain", null, setup);
            _proxy =
                (Proxy) domain.CreateInstanceFromAndUnwrap(typeof (Proxy).Assembly.Location, typeof (Proxy).FullName);

            _proxy.InstantiateObject(exePath, "OpenDental.FormChooseDatabase", null);
            // Set these fields to make it load values from config file
            _proxy.SetField("WebServiceUri", "");
            _proxy.SetField("DatabaseName", "");
            _proxy.InvokeMethod("GetConfig", null);
            //_proxy.InvokeMethod("GetCmdLine", null);

            _initialized = (bool) _proxy.InvokeMethod("TryToConnect", null);

            AssertApiInitialized();

            //_procedureCodes = ProcedureCodes.GetAllCodes().ToDictionary(m => m.CodeNum, m => m.ProcCode);
            _procedureCodes = _proxy.GetAllCodes().ToDictionary(m => m.CodeNum, m => m.ProcCode);
        }

        public Api.Common.PatientInfo GetPatientInfo(string patientId)
        {
            AssertApiInitialized();

            //var patient = Patients.GetPat(long.Parse(patientId));
            var patient = _proxy.GetPatient(long.Parse(patientId));
            if (patient != null)
            {
                return new Api.Common.PatientInfo
                {
                    BirthDate = patient.Birthdate,
                    FirstName = patient.FName,
                    LastName = patient.LName
                };
            }
            return null;
        }

        private void AssertApiInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException(@"OpenDental API failed to initialize");
        }

        public List<Api.Common.ProcedureInfo> GetProcedures(string patientId)
        {
            AssertApiInitialized();

            //var procedures = Procedures.GetCompleteForPats(new List<long> {long.Parse(patientId)});
            var procedures = _proxy.GetProcedures(long.Parse(patientId));
            if (procedures != null && procedures.Any())
            {
                return procedures.Select(p => new Api.Common.ProcedureInfo
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
            AssertApiInitialized();

            // Find insurance plan by carrier
            //var plan = InsPlans.GetByCarrierName(InsuranceCarrierName);
            var plan = _proxy.GetInsPlanByCarrier(InsuranceCarrierName);

            // Find appointments by insurance plan, dates, status
            var dateRange = GetTimeRangeForToday();
            //var appointments = Appointments.GetAppointmentsStartingWithinPeriod(dateRange.Item1, dateRange.Item2);
            var appointments = _proxy.GetAppointmentsStartingWithinPeriod(dateRange.Item1, dateRange.Item2);
            return new List<IAppointment>(appointments.Where(m => m.AptStatus != ApptStatus.UnschedList && m.AptStatus != ApptStatus.Broken
            && (m.InsPlan1 == plan.PlanNum || m.InsPlan2 == plan.PlanNum))
                .Select(m =>
                {
                    var patient = _proxy.GetPatient(m.PatNum);
                    var appointment = new Appointment
                    {
                        Date = m.AptDateTime,
                        InsuranceId = plan.PlanNum.ToString(),
                        IsCompleted = m.AptStatus == ApptStatus.Complete,
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
            AssertApiInitialized();

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
            return FileVersionInfo.GetVersionInfo(_path).ProductVersion;
        }
    }
}
