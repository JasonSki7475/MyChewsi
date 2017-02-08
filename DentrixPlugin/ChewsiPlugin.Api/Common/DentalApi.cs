using System;

namespace ChewsiPlugin.Api.Common
{
    public abstract class DentalApi
    {
        //TODO
        protected const string InsuranceCarrierName =
            //"PRINCIPAL";// value for Dentrix G with test database
            "Chewsi";// value for OpenDental

        protected Tuple<DateTime, DateTime> GetTimeRangeForToday()
        {
            var now = DateTime.Now;

            /*var dateStart = new DateTime(1993, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(1996, 6, 1, 23, 59, 59);
 */
            /*var dateStart = new DateTime(2012, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2012, 6, 1, 23, 59, 59);*/

            var dateStart = new DateTime(2017, 1, 1, 23, 59, 59);
            var dateEnd = new DateTime(2017, 3, 1, 23, 59, 59);

            /*
            var dateStart = now.Date;
            var dateEnd = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);
             */
            return new Tuple<DateTime, DateTime>(dateStart, dateEnd);
        }
    }
}
