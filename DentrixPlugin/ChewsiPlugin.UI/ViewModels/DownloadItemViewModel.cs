using System;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    public class DownloadItemViewModel : ViewModelBase
    {
        private readonly string _url;
        private readonly string _reportUrl;
        private readonly string _payeeId;
        private readonly DateTime _postedOnDate;

        public DownloadItemViewModel(string url, string reportUrl, string payeeId, DateTime postedOnDate)
        {
            _url = url;
            _reportUrl = reportUrl;
            _payeeId = payeeId;
            _postedOnDate = postedOnDate;
        }

        public string Url
        {
            get { return _url; }
        }

        public string ReportUrl
        {
            get { return _reportUrl; }
        }

        public string PayeeId
        {
            get { return _payeeId; }
        }

        public DateTime PostedOnDate
        {
            get { return _postedOnDate; }
        }
    }
}
