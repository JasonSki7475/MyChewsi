using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
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
        private readonly IClaimStoreService _claimStoreService;
        private DateTime _date;
        private string _chewsiId;
        private string _patient;
        private string _providerId;
        private string _patientId;
        private string _statusText;
        private AppointmentState _state;
        private ICommand _submitCommand;
        private ICommand _deleteCommand;

        public static class StatusMessage
        {
            public const string PaymentProcessing = "Payment processing...";
            public const string ReadyToSubmit = "Please submit this claim..";
        }

        public ClaimItemViewModel(IAppService appService, IClaimStoreService claimStoreService)
        {
            _appService = appService;
            _claimStoreService = claimStoreService;
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

        public string Patient
        {
            get { return _patient; }
            set
            {
                _patient = value;
                RaisePropertyChanged(() => Patient);
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
            StatusText = StatusMessage.PaymentProcessing;

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                ProviderInformation providerInformation;
                SubscriberInformation subscriberInformation;
                Provider provider;
                var validationResponse = _appService.ValidateClaim(ProviderId, PatientId, out providerInformation, out subscriberInformation, out provider);
                if (validationResponse != null)
                {
                    if (validationResponse.ValidationPassed)
                    {
                        _appService.SubmitClaim(PatientId, providerInformation, subscriberInformation, provider);
                    }
                    else
                    {
                        StatusText = $"{validationResponse.ProviderValidationMessage}{Environment.NewLine}{validationResponse.SubscriberValidationMessage}";
                        if (validationResponse.SubscriberNoLongerActive || validationResponse.ProviderNotFound)
                        {
                            State = AppointmentState.ValidationErrorUnrecoverable;
                        }
                        else
                        {
                            State = AppointmentState.ValidationError;
                        }
                        _appService.UpdateCachedClaim(ChewsiId, Date, State, StatusText);
                    }                    
                }
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
            _claimStoreService.DeleteAppointment(ChewsiId, Date);
        }
        #endregion
        #endregion
    }
}