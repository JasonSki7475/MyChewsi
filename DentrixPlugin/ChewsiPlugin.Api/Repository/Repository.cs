using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Dapper;

namespace ChewsiPlugin.Api.Repository
{
    public class Repository : IRepository
    {
        private readonly string _databaseFilePath;
        private const string Password = "vAekLEYNnuv239hwNAX2";
        private const string SettingsFolderName = "Chewsi"; // The same folder is specified in NLog.config
        private const string DatabaseFileName = "Database.sqlite";

        public Repository()
        {
            // c:\ProgramData\Chewsi
            _databaseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), SettingsFolderName, DatabaseFileName);
        }

        private SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection($"Data Source={_databaseFilePath};Password={Password};");
            connection.Open();
            return connection;
        }
        
        public void AddAppointment(Appointment item)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"INSERT INTO Appointments (Id, ChewsiId, DateTime, State, StatusText, PatientName, ProviderId, PatientId, SubscriberFirstName, PmsModifiedDate) VALUES (@Id, @ChewsiId, @DateTime, @State, @StatusText, @PatientName, @ProviderId, @PatientId, @SubscriberFirstName, @PmsModifiedDate)", item);
            }
        }

        public T GetSettingValue<T>(string key)
        {
            using (var connection = GetConnection())
            {
                var query = @"SELECT Value FROM Settings WHERE Key = @Key";
                if (typeof(T).IsEnum)
                {
                    var value = connection.ExecuteScalar<string>(query, new { Key = key });
                    return (T)Enum.Parse(typeof (T), value);
                }
                return connection.ExecuteScalar<T>(query, new { Key = key });
            }
        }
        
        public Appointment GetAppointmentById(string id)
        {
            using (var connection = GetConnection())
            {
                return connection.QueryFirstOrDefault<Appointment>(@"SELECT * FROM Appointments WHERE Id = @Id", new { Id = id });
            }
        }

        public void AddDeletedStatus(string providerId, string chewsiId, DateTime date)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"INSERT INTO DeletedStatuses (ProviderId, ChewsiId, Date) VALUES (@ProviderId, @ChewsiId, @Date)",
                    new
                    {
                        ProviderId = providerId,
                        ChewsiId = chewsiId,
                        Date = date
                    });
            }
        }

        public bool DeletedStatusExists(string providerId, string chewsiId, DateTime date)
        {
            using (var connection = GetConnection())
            {
                return connection.ExecuteScalar<int>(@"SELECT 1 FROM DeletedStatuses WHERE ProviderId = @ProviderId AND ChewsiId=@ChewsiId AND Date=@Date",
                    new
                    {
                        ProviderId = providerId,
                        ChewsiId = chewsiId,
                        Date = date
                    }) == 1;
            }
        }

        public void BulkDeleteDeletedStatuses(List<string> ids)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"DELETE FROM DeletedStatuses WHERE Id IN (@Id)", ids.Select(i => new { Id = i }).ToList());
            }
        }

        public List<DeletedStatus> GetDeletedStatuses()
        {
            using (var connection = GetConnection())
            {
                return connection.Query<DeletedStatus>(@"SELECT * FROM DeletedStatuses").ToList();
            }
        }

        public List<Appointment> GetAppointments()
        {
            using (var connection = GetConnection())
            {
                return connection.Query<Appointment>(@"SELECT * FROM Appointments").ToList();
            }
        }

        public void UpdateAppointment(Appointment item)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"UPDATE Appointments SET ChewsiId = @ChewsiId, DateTime = @DateTime, State = @State, StatusText = @StatusText, PatientName=@PatientName, ProviderId=@ProviderId, PatientId=@PatientId, SubscriberFirstName=@SubscriberFirstName, PmsModifiedDate=@PmsModifiedDate WHERE Id = @Id", item);
            }
        }

        public void BulkDeleteAppointments(List<string> ids)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"DELETE FROM Appointments WHERE Id IN (@Id)", ids.Select(i => new { Id = i }).ToList());
            }
        }

        public bool Ready => File.Exists(_databaseFilePath)
                                   && TablesExist()
                                   && GetSettingValue<string>(Settings.PMS.TypeKey) != null
                                   && GetSettingValue<string>(Settings.PMS.PathKey) != null
                                   && GetSettingValue<string>(Settings.MachineIdKey) != null;

        private bool TablesExist()
        {
            using (var connection = GetConnection())
            {
                return connection.ExecuteScalar<int>(@"SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Settings'") == 1;
            }
        }

        public void Initialize()
        {
            if (!File.Exists(_databaseFilePath))
            {
                SQLiteConnection.CreateFile(_databaseFilePath);
            }
            
            using (var connection = GetConnection())
            {
                if (!TablesExist())
                {
                    connection.Execute(
                        @"create table Settings
                                  (
                                     Key        TEXT primary key,
                                     Value      TEXT null
                                  )");
                    connection.Execute(
                        @"create table Appointments
                                  (
                                     Id                 TEXT primary key not null,
                                     ChewsiId           TEXT not null,
                                     PatientId          TEXT null,
                                     PatientName        TEXT not null,
                                     SubscriberFirstName    TEXT not null,
                                     ProviderId         TEXT not null,
                                     StatusText         TEXT null,
                                     DateTime           DATETIME not null,
                                     PmsModifiedDate    DATETIME not null,
                                     State              INTEGER not null
                                  )");
                    connection.Execute(
                        @"create table SubmittedProcedures
                                  (
                                     Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                                     PatientId          TEXT not null,
                                     ProviderId         TEXT not null,
                                     Date               DATETIME not null,
                                     Code               TEXT not null,
                                     Amount             REAL not null
                                  )");
                    connection.Execute(
                        @"create table DeletedStatuses
                                  (
                                     Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                                     ProviderId         TEXT not null,
                                     ChewsiId           TEXT not null,
                                     Date               DATETIME not null
                                  )");
                }
            }
        }

        public IEnumerable<SubmittedProcedure> GetSubmittedProcedures(string patientId, string providerId, DateTime date)
        {
            using (var connection = GetConnection())
            {
                return connection.Query<SubmittedProcedure>(@"SELECT * FROM SubmittedProcedures WHERE PatientId = @PatientId AND Date = @Date AND ProviderId=@ProviderId", 
                    new
                    {
                        PatientId = patientId,
                        Date = date,
                        ProviderId = providerId
                    });
            }
        }

        public List<SubmittedProcedure> GetSubmittedProcedures()
        {
            using (var connection = GetConnection())
            {
                return connection.Query<SubmittedProcedure>(@"SELECT * FROM SubmittedProcedures").ToList();
            }
        }

        public void AddSubmittedProcedures(IEnumerable<SubmittedProcedure> procedures)
        {
            using (var connection = GetConnection())
            {
                foreach (var procedure in procedures)
                {
                    connection.Execute(@"INSERT INTO SubmittedProcedures (PatientId, Date, Code, Amount, ProviderId) VALUES (@PatientId, @Date, @Code, @Amount, @ProviderId)", procedure);
                }
            }
        }

        public void BulkDeleteSubmittedProcedures(List<int> ids)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"DELETE FROM SubmittedProcedures WHERE Id IN (@Id)", ids.Select(i => new { Id = i }).ToList());
            }
        }

        public void SaveSetting(string key, object value)
        {
            using (var connection = GetConnection())
            {
                if (connection.ExecuteScalar<string>(@"SELECT Key FROM Settings WHERE Key = @Key", new { Key = key }) == null)
                {
                    connection.Execute(@"INSERT INTO Settings (Key, Value) VALUES (@Key, @Value)", new Setting { Key = key, Value = value?.ToString() });
                }
                else
                {
                    connection.Execute(@"UPDATE Settings SET Value = @Value WHERE Key = @Key", new Setting { Key = key, Value = value?.ToString() });
                }
            }
        }
    }
}
