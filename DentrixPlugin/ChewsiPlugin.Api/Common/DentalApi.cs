using System;
using ChewsiPlugin.Api.Interfaces;
using NLog;

namespace ChewsiPlugin.Api.Common
{
    public abstract class DentalApi
    {
        protected IDialogService _dialogService;
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        protected bool _initialized;

        //TODO
        protected const string InsuranceCarrierName =
            //"PRINCIPAL";// value for Dentrix G with test database (G6.1)
            "Chewsi";// value for OpenDental

        protected Tuple<DateTime, DateTime> GetTimeRangeForToday()
        {
            var now = DateTime.Now;
            /*
                        var dateStart = new DateTime(1993, 1, 1, 23, 59, 59);
                        var dateEnd = new DateTime(1996, 6, 1, 23, 59, 59);
            

            // values for Dentrix G with test database (G6.1)
            var dateStart = new DateTime(2012, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2012, 6, 1, 23, 59, 59);
*/
            var dateStart = new DateTime(2017, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2017, 3, 1, 23, 59, 59);
/*
            
            var dateStart = now.Date;
            var dateEnd = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);
             */
            return new Tuple<DateTime, DateTime>(dateStart, dateEnd);
        }
        
        public bool Initialized { get { return _initialized; } }
    }
}
