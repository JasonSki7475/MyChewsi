using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;

namespace ChewsiPlugin.UI.ViewModels
{
    public class ClaimItemViewModel : ViewModelBase
    {
        private readonly IAppService _appService;
        private DateTime _date;
        private string _chewsiId;
        private string _patientName;
        private string _subscriberFirstName;
        private string _providerId;
        private string _claimNumber;
        private string _patientId;
        private string _statusText;
        private bool _isBeingSubmitted;
        private AppointmentState _state;
        private ICommand _submitCommand;
        private ICommand _deleteCommand;
        private bool _isClaimStatus;

        public ClaimItemViewModel(IAppService appService)
        {
            _appService = appService;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is an appointment from the PMS or a claim status returned back from the Chewsi server
        /// </summary>
        public bool IsClaimStatus
        {
            get { return _isClaimStatus; }
            set
            {
                _isClaimStatus = value;
                RaisePropertyChanged(() => IsClaimStatus);
            }
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

        public string SubscriberFirstName
        {
            get { return _subscriberFirstName; }
            set
            {
                _subscriberFirstName = value;
                RaisePropertyChanged(() => SubscriberFirstName);
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

        public string ClaimNumber
        {
            get { return _claimNumber; }
            set
            {
                _claimNumber = value;
                RaisePropertyChanged(() => ClaimNumber);
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

        public bool IsBeingSubmitted
        {
            get { return _isBeingSubmitted; }
            set
            {
                _isBeingSubmitted = value;
                RaisePropertyChanged(() => IsBeingSubmitted);
            }
        }

        public bool CanResubmit
        {
            get { return !IsClaimStatus && State == AppointmentState.ValidationError; }
        }
        public bool ShowErrorView
        {
            get { return State == AppointmentState.ValidationError || State == AppointmentState.ValidationErrorNoResubmit; }
        }

        public AppointmentState State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged(() => State);
                RaisePropertyChanged(() => CanResubmit);
                RaisePropertyChanged(() => ShowErrorView);
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
            return _appService.Initialized && !IsBeingSubmitted;
        }

        private void OnSubmitCommandExecute()
        {
            IsBeingSubmitted = true;
            _appService.ValidateAndSubmitClaim(ChewsiId, Date, ProviderId, PatientId, () =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    IsBeingSubmitted = false;
                });
            });
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