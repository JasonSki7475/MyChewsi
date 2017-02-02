using System;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    public class ClaimItemViewModel : ViewModelBase
    {
        private DateTime _date;
        private string _insuranceId;
        private string _subscriber;
        private string _provider;
        private string _patientId;
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

        public string InsuranceId
        {
            get { return _insuranceId; }
            set
            {
                _insuranceId = value;
                RaisePropertyChanged(() => InsuranceId);
            }
        }

        public string Subscriber
        {
            get { return _subscriber; }
            set
            {
                _subscriber = value;
                RaisePropertyChanged(() => Subscriber);
            }
        }

        public string Provider
        {
            get { return _provider; }
            set
            {
                _provider = value;
                RaisePropertyChanged(() => Provider);
            }
        }

        public string PatientId
        {
            get { return _patientId; }
            set
            {
                _patientId = value;
                RaisePropertyChanged(() => PatientId);
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