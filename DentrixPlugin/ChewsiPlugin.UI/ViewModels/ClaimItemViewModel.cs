using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class ClaimItemViewModel : ViewModelBase
    {
        private readonly IClientAppService _appService;
        private readonly IPaymentsCalculationViewModel _paymentsCalculationViewModel;
        private DateTime _date;
        private string _chewsiId;
        private string _patientName;
        private string _subscriberFirstName;
        private string _providerId;
        private string _claimNumber;
        private string _patientId;
        private string _statusText;
        private AppointmentState _state;
        private ICommand _submitCommand;
        private ICommand _calculatePaymentsCommand;
        private ICommand _deleteCommand;
        private bool _isClaimStatus;
        private string _id;
        private int _numberOfPayments;
        private double _downPayment;
        private DateTime _firstMonthlyPaymentDate;
        private DateTime _pmsModifiedDate;
        private bool _locked;
        private bool _isCptError;
        private bool _eligibleForPayments;

        public ClaimItemViewModel(IClientAppService appService, IPaymentsCalculationViewModel paymentsCalculationViewModel)
        {
            _appService = appService;
            _paymentsCalculationViewModel = paymentsCalculationViewModel;
        }

        public static readonly List<Tuple<int, string>> NumberOfPaymentsList = new List<Tuple<int, string>>(
            new[]
            {
                new Tuple<int, string>(1, "Pay In Full"),
                new Tuple<int, string>(6, "6"),
                new Tuple<int, string>(12, "12"),
                new Tuple<int, string>(18, "18"),
                new Tuple<int, string>(24, "24"),
                new Tuple<int, string>(30, "30"),
                new Tuple<int, string>(36, "36")
            });

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

        /// <summary>
        /// Gets or sets a value indicating whether this instance is a claim status with invalid (or obsolete, unsupported) Procedure Code
        /// </summary>
        public bool IsCptError
        {
            get { return _isCptError; }
            set
            {
                _isCptError = value;
                RaisePropertyChanged(() => IsCptError);
            }
        }
        
        public bool CanDelete => !IsClaimStatus || IsCptError;

        public string Id
        {
            get { return _id; }
            set
            {
                _id = value;
                RaisePropertyChanged(() => Id);
            }
        }

        public int NumberOfPayments
        {
            get { return _numberOfPayments; }
            set
            {
                _numberOfPayments = value;
                RaisePropertyChanged(() => NumberOfPayments);
                RaisePropertyChanged(() => NumberOfPaymentsItem);
            }
        }

        public Tuple<int, string> NumberOfPaymentsItem
        {
            get { return NumberOfPaymentsList.First(m => m.Item1 == NumberOfPayments); }
            set
            {
                NumberOfPayments = value.Item1;
            }
        }

        public double DownPayment
        {
            get { return _downPayment; }
            set
            {
                _downPayment = value;
                RaisePropertyChanged(() => DownPayment);
            }
        }

        public DateTime FirstMonthlyPaymentDate
        {
            get { return _firstMonthlyPaymentDate; }
            set
            {
                _firstMonthlyPaymentDate = value;
                RaisePropertyChanged(() => FirstMonthlyPaymentDate);
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

        public DateTime PmsModifiedDate
        {
            get { return _pmsModifiedDate; }
            set
            {
                _pmsModifiedDate = value;
                RaisePropertyChanged(() => PmsModifiedDate);
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

        public bool EligibleForPayments
        {
            get { return _eligibleForPayments; }
            set
            {
                _eligibleForPayments = value;
                RaisePropertyChanged(() => EligibleForPayments);
            }
        }

        public bool CanResubmit => State == AppointmentState.ValidationError;
        public bool ShowErrorView => State == AppointmentState.ValidationError || State == AppointmentState.ValidationErrorNoResubmit;

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
            if (IsClaimStatus ^ item.IsClaimStatus)
            {
                return false;
            }
            return Date == item.Date
                   && PatientName == item.PatientName
                   && ClaimNumber == item.ClaimNumber
                   && SubscriberFirstName == item.SubscriberFirstName
                   && ChewsiId == item.ChewsiId
                   && ProviderId == item.ProviderId;
        }

        #region Commands

        #region CalculatePaymentsCommand
        public ICommand CalculatePaymentsCommand => _calculatePaymentsCommand ?? (_calculatePaymentsCommand = new RelayCommand(OnCalculatePaymentsCommandExecute));

        private void OnCalculatePaymentsCommandExecute()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                var result = _appService.GetCalculatedPayments(Id, DownPayment, NumberOfPayments, FirstMonthlyPaymentDate);
                if (result != null)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        _paymentsCalculationViewModel.Show(result);
                    });
                }
            };
            worker.RunWorkerAsync();
        }

        #endregion

        #region SubmitCommand
        public ICommand SubmitCommand => _submitCommand ?? (_submitCommand = new RelayCommand(OnSubmitCommandExecute, CanSubmitCommandExecute));

        private bool CanSubmitCommandExecute()
        {
            return _appService.Initialized && !Locked;
        }

        private void OnSubmitCommandExecute()
        {
            Lock();
            _appService.ValidateAndSubmitClaim(Id, DownPayment, NumberOfPayments);
            Unlock();
        }
        #endregion

        #region DeleteCommand
        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(OnDeleteCommandExecute));

        private void OnDeleteCommandExecute()
        {
            if (IsCptError)
            {
                // ProviderId can be empty for old statuses, user needs to refresh the list
                if (!string.IsNullOrEmpty(ProviderId))
                {
                    _appService.DeleteClaimStatus(ProviderId, ChewsiId, Date);
                }
            }
            else
            {
                _appService.DeleteAppointment(Id);
            }
        }
        #endregion

        #endregion

        public void Lock()
        {
            Locked = true;
        }

        public void Unlock()
        {
            Locked = false;
        }

        public bool Locked
        {
            get { return _locked; }
            set
            {
                _locked = value;
                RaisePropertyChanged(() => Locked);
            }
        }
    }
}