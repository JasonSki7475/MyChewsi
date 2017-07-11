using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using Dapper;
using Appointment = ChewsiPlugin.Api.Common.Appointment;
using PatientInfo = ChewsiPlugin.Api.Common.PatientInfo;
using ProcedureInfo = ChewsiPlugin.Api.Common.ProcedureInfo;

namespace ChewsiPlugin.OpenDentalApi
{
    /// <summary>
    /// Second version of OpenDental API. Uses direct SQL queries to OD database (MySQL).
    /// </summary>
    /// <seealso cref="OpenDentalApi" />
    public class OpenDentalApi2 : DentalApi, IDentalApi
    {
        private readonly IRepository _repository;
        private string _openDentalInstallationDirectory;
        private const string OpenDentalBusinessName = @"OpenDentBusiness.dll";
        private string _connectionString;
        
        public OpenDentalApi2(IRepository repository)
        {
            _repository = repository;
        }

        public PatientInfo GetPatientInfo(string patientId)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return
                    connection.QueryFirstOrDefault<PatientInfo>(
                        "SELECT pat.Birthdate AS BirthDate, pat.LName as PatientLastName, pat.FName as PatientFirstName, " +
                        "inssub.SubscriberID as ChewsiId, subscriber.LName as SubscriberLastName, subscriber.FName as SubscriberFirstName " +
                        "FROM patient pat LEFT JOIN patplan ON patplan.PatNum = pat.PatNum " +
                        "LEFT JOIN inssub ON inssub.InsSubNum = patplan.InsSubNum " +
                        "JOIN patient subscriber ON subscriber.PatNum = pat.Guarantor " +
                        $"WHERE pat.PatNum = {patientId}");
            }
        }

        private MySql.Data.MySqlClient.MySqlConnection GetConnection()
        {
            var connection = new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void Initialize()
        {
            if (!_initialized)
            {
                _connectionString = _repository.GetSettingValue<string>(Settings.PMS.ConnectionStringKey);
                _openDentalInstallationDirectory = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
                _initialized = true;
            }
        }

        public string GetConnectionString()
        {
            try
            {
                string path;
                if (TryGetFolder(out path))
                {
                    path = Path.Combine(path, "FreeDentalConfig.xml");
                    Logger.Info("Loading connection string");
                    if (File.Exists(path))
                    {
                        var doc = new XmlDocument();
                        doc.Load(path);
                        var cs = doc.SelectSingleNode("//ConnectionString")?.InnerText;
                        if (cs == null)
                        {
                            var host = doc.SelectSingleNode("//ConnectionSettings/DatabaseConnection/ComputerName")?.InnerText;
                            var db = doc.SelectSingleNode("//ConnectionSettings/DatabaseConnection/Database")?.InnerText;
                            var user = doc.SelectSingleNode("//ConnectionSettings/DatabaseConnection/User")?.InnerText;
                            var password = doc.SelectSingleNode("//ConnectionSettings/DatabaseConnection/Password")?.InnerText;       
                            cs = $"Server={host};Database={db};Uid={user};Pwd={password};";                  
                        }
                        Logger.Info("Connection string loaded");
                        return cs;
                    }
                    Logger.Error("Cannot find file FreeDentalConfig.xml");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Cannot load connection string");
                throw;
            }
            return null;
        }

        public List<ProcedureInfo> GetProcedures(string patientId, string appointmentId, DateTime appointmentDate)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var procedures =
                    connection.Query<ProcedureInfo>(
                        "SELECT p.ProcDate as Date, pc.ProcCode as Code, p.ProcFee as Amount " +
                        "FROM procedurelog p " +
                        "JOIN procedurecode pc ON pc.CodeNum = p.CodeNum " +
                        $"WHERE ProcStatus = 2 AND (AptNum={appointmentId} OR PlannedAptNum={appointmentId})")
                        .ToList();
                procedures.ForEach(m => m.Code = m.Code.TrimStart(ProcedureCodeFirstCharToTrim));
                return procedures;
            }
        }
        
        public override List<Appointment> GetAppointments(DateTime date)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var dateRange = GetTimeRangeForToday(date);
                var appts = connection.Query<Appointment>("SELECT a.AptNum as Id, a.DateTStamp as PmsModifiedDate, a.AptDateTime as Date, a.PatNum as PatientId, a.ProvNum as ProviderId " +
                                                          "FROM appointment a " +
                                                          "JOIN insplan i ON a.InsPlan1 = i.PlanNum OR a.InsPlan2 = i.PlanNum " +
                                                          "JOIN carrier c ON c.CarrierNum = i.CarrierNum " +
                                                          $"WHERE a.AptDateTime >= '{dateRange.Item1.ToString("yyyy-MM-dd HH:mm:ss")}' AND a.AptDateTime <= '{dateRange.Item2.ToString("yyyy-MM-dd HH:mm:ss")}' " +
                                                          "AND a.AptStatus = 2 " +
                                                          $"AND c.CarrierName = '{InsuranceCarrierName}'").ToList();
                var patientIds = appts.Select(m => m.PatientId).Distinct();
                var patientInfos = patientIds.ToDictionary(m => m, GetPatientInfo);

                foreach (var appt in appts)
                {
                    var patient = patientInfos[appt.PatientId];
                    if (patient != null)
                    {
                        appt.ChewsiId = patient.ChewsiId;
                        appt.PatientName = $"{patient.PatientLastName}, {patient.PatientFirstName}";                        
                    }
                }
                return appts;
            }
        }

        public Provider GetProvider(string providerId)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                return connection.QueryFirstOrDefault<Provider>($"SELECT NationalProvID as Npi, StateWhereLicensed as State, SSN as Tin FROM provider WHERE ProvNum={providerId}");
            }
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
                    Logger.Info("Found OpenDental API in '{0}'", folder);
                    return true;
                }
            }
            Logger.Error("Cannot find OpenDental folder");
            folder = null;
            return false;
        }

        public Appointment GetAppointmentById(string id)
        {
            Initialize();

            using (var connection = GetConnection())
            {
                var appt = connection.QueryFirstOrDefault<Appointment>("SELECT AptNum as Id, DateTStamp as PmsModifiedDate, AptDateTime as Date, " +
                                                                       $"PatNum as PatientId, ProvNum as ProviderId FROM appointment WHERE AptNum={id})");
                if (appt != null)
                {
                    var patient = GetPatientInfo(appt.PatientId);
                    appt.ChewsiId = patient.ChewsiId;
                    appt.PatientName = $"{patient.PatientLastName}, {patient.PatientFirstName}";
                    return appt;
                }
            }
            return null;
        }

        public void Unload()
        {
        }

        protected override string PmsExeRelativePath => "OpenDental.exe";
    }
}