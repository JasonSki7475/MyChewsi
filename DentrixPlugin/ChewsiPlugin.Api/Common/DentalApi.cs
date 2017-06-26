using System;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace ChewsiPlugin.Api.Common
{
    public abstract class DentalApi
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        protected bool _initialized;

        protected const char ProcedureCodeFirstCharToTrim = 'D';

        protected const string InsuranceCarrierName =
            //"PRINCIPAL";// value for Dentrix G with test database (G6.1)
            "Chewsi"; // value for OpenDental

        protected Tuple<DateTime, DateTime> GetTimeRangeForToday(DateTime date)
        {
            /*
            var dateStart = new DateTime(1993, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(1996, 6, 1, 23, 59, 59);
            
            // values for Dentrix G with test database (G6.1)
            var dateStart = new DateTime(2012, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2012, 6, 1, 23, 59, 59);

            var dateStart = new DateTime(2017, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2017, 7, 1, 23, 59, 59);
            */

            var dateStart = date.Date;
            var dateEnd = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);

            return new Tuple<DateTime, DateTime>(dateStart, dateEnd);
        }

        public abstract bool TryGetFolder(out string folder);
        protected abstract string PmsExeRelativePath { get; }
        public abstract List<Appointment> GetAppointments(DateTime date);

        public bool IsInitialized()
        {
            return _initialized;
        }

        public string GetPmsExecutablePath(string pmsFolder)
        {
            string folder;
            if (pmsFolder != null)
            {
                folder = pmsFolder;
            }
            else
            {
                TryGetFolder(out folder);
            }
            if (folder != null)
            {
                return Path.Combine(folder, PmsExeRelativePath);
            }
            return null;
        }
    }
}