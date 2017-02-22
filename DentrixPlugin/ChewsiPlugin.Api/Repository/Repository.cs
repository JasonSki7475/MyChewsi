using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
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
                connection.Execute(@"INSERT INTO Appointments (Id, ChewsiId, DateTime, Deleted) VALUES (@Id, @ChewsiId, @DateTime, @Deleted)", item);
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

        public IEnumerable<Appointment> GetAppointments()
        {
            using (var connection = GetConnection())
            {
                return connection.Query<Appointment>(@"SELECT * FROM Appointments");
            }
        }

        public void UpdateAppointment(Appointment item)
        {
            using (var connection = GetConnection())
            {
                connection.Execute(@"UPDATE Appointments SET ChewsiId = @ChewsiId, DateTime = @DateTime, Deleted = @Deleted WHERE Id = @Id", item);
            }
        }

        public bool Initialized()
        {
            return File.Exists(_databaseFilePath)
                && GetSettingValue<string>(Settings.PMS.TypeKey) != null
                && GetSettingValue<string>(Settings.PMS.PathKey) != null;
        }

        public void Initialize()
        {
            if (!File.Exists(_databaseFilePath))
            {
                SQLiteConnection.CreateFile(_databaseFilePath);
                // Create tables
                using (var connection = GetConnection())
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
                                 Id         TEXT primary key,
                                 ChewsiId   TEXT not null,
                                 DateTime   DATETIME not null,
                                 Deleted    boolean not null
                              )");
                }
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
