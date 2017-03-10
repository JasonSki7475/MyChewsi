using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ChewsiPlugin.UI.ViewModels
{
    public class ClaimItemViewModel : ViewModelBase
    {
        private readonly IAppService _appService;
        private DateTime _date;
        private DateTime? _submissionDate;
        private string _chewsiId;
        private string _patientName;
        private string _providerId;
        private string _patientId;
        private string _statusText;
        private AppointmentState _state;
        private ICommand _submitCommand;
        private ICommand _deleteCommand;

        public ClaimItemViewModel(IAppService appService)
        {
            _appService = appService;
        }

        public DateTime Date
        {
            get { return _date; }
            set
            {
                _date = value;
                RaisePropertyChanged(() => Date);
            }
        }

        public DateTime? SubmissionDate
        {
            get { return _submissionDate; }
            set
            {
                _submissionDate = value;
                RaisePropertyChanged(() => SubmissionDate);
            }
        }

        public string ChewsiId
        {
            get { return _chewsiId; }
            set
            {
                _chewsiId = value;
                RaisePropertyChanged(() => ChewsiId);
            }
        }

        public string PatientName
        {
            get { return _patientName; }
            set
            {
                _patientName = value;
                RaisePropertyChanged(() => PatientName);
            }
        }

        public string ProviderId
        {
            get { return _providerId; }
            set
            {
                _providerId = value;
                RaisePropertyChanged(() => ProviderId);
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

        public string StatusText
        {
            get { return _statusText; }
            set
            {
                _statusText = value;
                RaisePropertyChanged(() => StatusText);
            }
        }

        public AppointmentState State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged(() => State);
            }
        }

        public bool Equals(ClaimItemViewModel item)
        {
            return Date == item.Date
                   && PatientId == item.PatientId
                   && ChewsiId == item.ChewsiId
                   && ProviderId == item.ProviderId;
        }

        #region Commands
        #region SubmitCommand
        public ICommand SubmitCommand
        {
            get { return _submitCommand ?? (_submitCommand = new RelayCommand(OnSubmitCommandExecute, CanSubmitCommandExecute)); }
        }

        private bool CanSubmitCommandExecute()
        {
            return _appService.Initialized;
        }

        private void OnSubmitCommandExecute()
        {
            _appService.ValidateAndSubmitClaim(ChewsiId, Date, ProviderId, PatientId);
        }
        #endregion

        #region DeleteCommand
        public ICommand DeleteCommand
        {
            get { return _deleteCommand ?? (_deleteCommand = new RelayCommand(OnDeleteCommandExecute)); }
        }

        private void OnDeleteCommandExecute()
        {
            _appService.DeleteAppointment(ChewsiId, Date);
        }
        #endregion
        #endregion
    }
}