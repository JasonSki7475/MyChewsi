using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace ChewsiPlugin.Api.Repository
{
    public class Repository : IRepository
    {
        private readonly ISettings _settings;
        private const string Password = "vAekLEYNnuv239hwNAX2";

        public Repository(ISettings settings)
        {
            _settings = settings;
        }

        private SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection($"Data Source={_settings.DatabaseFilePath};Password={Password};");
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
                return connection.ExecuteScalar<T>(@"SELECT Value FROM Settings WHERE Key = @Key", new { Key = key });
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
            return File.Exists(_settings.DatabaseFilePath)
                && GetSettingValue<string>(Settings.PMS.TypeKey) != null
                && GetSettingValue<string>(Settings.PMS.PathKey) != null;
        }

        public void Initialize()
        {
            if (!File.Exists(_settings.DatabaseFilePath))
            {
                SQLiteConnection.CreateFile(_settings.DatabaseFilePath);
                // Create tables
                using (var connection = GetConnection())
                {
                    connection.Execute(
                        @"create table Settings
                              (
                                 Key        TEXT primary key,
                                 Value      TEXT not null
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
                if (connection.ExecuteScalar<string>(@"SELECT Value FROM Settings WHERE Key = @Key", new { Key = key }) == null)
                {
                    connection.Execute(@"INSERT INTO Settings (Key, Value) VALUES (@Key, @Value)", new Setting { Key = key, Value = value.ToString() });
                }
                else
                {
                    connection.Execute(@"UPDATE Settings SET Value = @Value WHERE Key = @Key", new Setting { Key = key, Value = value.ToString() });
                }
            }
        }
    }
}
