using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace ChewsiPlugin.Api.Repository
{
    public class Repository : IRepository
    {
        private const string Password = "vAekLEYNnuv239hwNAX2";

        public Repository()
        {
            if (!File.Exists(Utils.DatabaseFilePath))
            {
                if (!Directory.Exists(Utils.SettingsFolder))
                {
                    Directory.CreateDirectory(Utils.SettingsFolder);
                }

                SQLiteConnection.CreateFile(Utils.DatabaseFilePath);

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

        private SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection($"Data Source={Utils.DatabaseFilePath};Password={Password};");
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
