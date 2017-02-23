using System;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    public class DownloadItemViewModel : ViewModelBase
    {
        private DateTime _date;
        private string _status;

        public DateTime Date
        {
            get { return _date; }
            set
            {
                _date = value;
                RaisePropertyChanged(() => Date);
            }
        }

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaisePropertyChanged(() => Status);
            }
        }
    }
}
