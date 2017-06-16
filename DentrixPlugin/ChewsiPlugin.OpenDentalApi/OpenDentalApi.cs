using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.OpenDentalApi.DTO;
using Appointment = ChewsiPlugin.Api.Common.Appointment;
using PatientInfo = ChewsiPlugin.Api.Common.PatientInfo;
using ProcedureInfo = ChewsiPlugin.Api.Common.ProcedureInfo;
using Provider = ChewsiPlugin.Api.Common.Provider;

namespace ChewsiPlugin.OpenDentalApi
{
    public class OpenDentalApi : DentalApi, IDentalApi
    {
        private readonly IRepository _repository;
        private string _openDentalInstallationDirectory;
        private const string OpenDentalExeName = @"OpenDental.exe";
        private const string OpenDentalBusinessName = @"OpenDentBusiness.dll";
        
        private Dictionary<long, string> _procedureCodes;
        private Proxy _proxy;
        private AppDomain _domain;
        private readonly CancellationTokenSource _tokenSource;

        public OpenDentalApi(IRepository repository)
        {
            _repository = repository;
            _tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(RenewLeaseLoop, _tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private void RenewLeaseLoop()
        {
            _tokenSource.Token.ThrowIfCancellationRequested();
            while (true)
            {
                Utils.SleepWithCancellation(_tokenSource.Token, 120*1000);
                _proxy?.RenewLease();
            }
        }

        public PatientInfo GetPatientInfo(string patientId)
        {
            Initialize();

            var id = long.Parse(patientId);
            return GetPatientInfo(id);
        }

        private PatientInfo GetPatientInfo(long patientId)
        {
            var patient = _proxy.GetPatient(patientId);
            if (patient != null)
            {
                // load subscriber if necessary
                var subscriber = GetSubscriberInfo(patientId);
                return new PatientInfo
                {
                    BirthDate = patient.Birthdate,
                    PatientFirstName = patient.FName,
                    PatientLastName = patient.LName,

                    SubscriberFirstName = subscriber?.PatientInfo.FName ?? "",
                    SubscriberLastName = subscriber?.PatientInfo.LName ?? "",
                    ChewsiId = subscriber?.ChewsiId ?? ""
                };
            }
            return null;            
        }

        private void Initialize()
        {
            if (!_initialized)
            {
                _openDentalInstallationDirectory = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
                // Create new app domain and load OpenDental assemblies, then we will load data from them
                AppDomainSetup setup = new AppDomainSetup
                {
                    ApplicationBase = _openDentalInstallationDirectory
                };
                _domain = AppDomain.CreateDomain("OpenDentalApiDomain", null, setup);
                _proxy = (Proxy)_domain.CreateInstanceFromAndUnwrap(typeof(Proxy).Assembly.Location, typeof(Proxy).FullName);
                Logger.Debug("Created proxy class");
                _proxy.InstantiateObject(Path.Combine(_openDentalInstallationDirectory, OpenDentalExeName), "OpenDental.FormChooseDatabase", null);
                // Set these fields to make it load values from config file
                _proxy.SetField("WebServiceUri", "");
                _proxy.SetField("DatabaseName", "");
                _proxy.InvokeMethod("GetConfig", null);
                //_proxy.InvokeMethod("GetCmdLine", null);
                Logger.Debug("Loaded " + OpenDentalExeName);

                _proxy.Initialize(Path.Combine(_openDentalInstallationDirectory, OpenDentalBusinessName));
                if ((bool) _proxy.InvokeMethod("TryToConnect", null))
                {
                    Logger.Info("Successfully initialized DB connection");
                    _procedureCodes = _proxy.GetAllCodes().ToDictionary(m => m.CodeNum, m => m.ProcCode);
                    _initialized = true;
                }
                else
                {
                    throw new InvalidOperationException(@"Failed to initialize OpenDental API");
                }
            }
        }

        public List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate)
        {
            Initialize();
            
            var procedures = _proxy.GetProceduresByAppointment(long.Parse(appointmentId));
            if (procedures != null && procedures.Any())
            {
                return procedures.Where(m => m.IsCompleted)
                    .Select(p => new ProcedureInfo
                    {
                        Amount = p.ProcFee,
                        Code = _procedureCodes[p.CodeNum],
                        Date = p.ProcDate
                    })
                    .ToList();
            }
            return null;
        }

        private SubscriberInfo GetSubscriberInfo(long patientId)
        {
            var a = _proxy.GetSubscribers(patientId);
            return a.FirstOrDefault();
        }

        public override List<Appointment> GetAppointments(DateTime date)
        {
            Initialize();

            // Find carrier by name 
            var carrierInfo = _proxy.CarriersGetSimilarNames(InsuranceCarrierName).FirstOrDefault(m => m.CarrierName == InsuranceCarrierName);
            if (carrierInfo != null)
            {
                var planNums = _proxy.InsPlansGetPlanNumsByCarrierNum(carrierInfo.CarrierNum);
                
                // Find appointments by insurance plan, dates, status
                var dateRange = GetTimeRangeForToday(date);
                var appointments = _proxy.GetAppointmentsStartingWithinPeriod(dateRange.Item1, dateRange.Item2);

                var filtered = appointments.Where(m => m.AptStatus == "Complete" && (planNums.Contains(m.InsPlan1) || planNums.Contains(m.InsPlan2))).ToList();
                var patientIds = filtered.Select(m => m.PatNum).Distinct();
                var patientInfos = patientIds.ToDictionary(m => m, GetPatientInfo);

                return new List<Appointment>(filtered
                    .Select(m =>
                    {
                        var patient = patientInfos[m.PatNum];
                        var appointment = new Appointment
                        {
                            Id = m.AptNum.ToString(),
                            PmsModifiedDate = m.DateTStamp,
                            Date = m.AptDateTime,
                            ChewsiId = patient.ChewsiId,
                            PatientId = m.PatNum.ToString(),
                            PatientName = $"{patient.PatientLastName}, {patient.PatientFirstName}",
                            ProviderId = m.ProvNum.ToString()
                        };
                        return appointment;
                    })
                    .ToList());                
            }
            return new List<Appointment>();
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();
            
            var provider = _proxy.GetProvider(long.Parse(providerId));
            if (provider != null)
            {
                return new Provider
                {
                    // AddressLine = ,
                    // City = ,
                    Npi = provider.NationalProvID,
                    State = provider.StateWhereLicensed,
                    Tin = provider.SSN,
                    // ZipCode = 
                };
            }
            return null;
        }

        public string GetVersion()
        {
            Initialize();

            return FileVersionInfo.GetVersionInfo(Path.Combine(_openDentalInstallationDirectory, OpenDentalBusinessName)).ProductVersion;
        }

        public override bool TryGetFolder(out string folder)
        {
            List<string> paths = new List<string>();
            foreach (var drive in DriveInfo.GetDrives().Where(m => m.IsReady && m.DriveType == DriveType.Fixed))
            {
                paths.Add($"{drive}Program Files (x86)\\Open Dental\\OpenDental.exe");
                paths.Add($"{drive}Program Files\\Open Dental\\OpenDental.exe");
            }
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    folder = Path.GetDirectoryName(path);
                    return true;
                }
            }
            folder = null;
            return false;
        }
        
        public void Unload()
        {
            _tokenSource.Cancel();
            if (_domain != null)
            {
                AppDomain.Unload(_domain);
                _domain = null;
            }
        }

        protected override string PmsExeRelativePath => "OpenDental.exe";
    }
}
