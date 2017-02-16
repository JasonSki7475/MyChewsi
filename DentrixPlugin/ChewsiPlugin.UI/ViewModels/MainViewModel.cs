using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ICommand _submitCommand;
        private ICommand _downloadReportCommand;
        private ICommand _downloadCommand;
        private ICommand _deleteCommand;
        private ICommand _refreshDownloadsCommand;
        private ICommand _refreshAppointmentsCommand;
        private ICommand _closeValidationPopupCommand;
        private ICommand _closeProcessingPaymentPopupCommand;
        private ClaimItemViewModel _selectedClaim;
        private bool _showValidationError;
        private bool _showPaymentProcessing;
        private string _paymentProcessingMessage;
        private bool _loadingAppointments;
        private string _validationError;
        private readonly IChewsiApi _chewsiApi;
        private readonly IAppLoader _appLoader;
        private IDentalApi _dentalApi;
        private readonly DialogService.IDialogService _dialogService;
        private bool _isBusy;
        private bool _showPmsSettings;

        public MainViewModel(DialogService.IDialogService dialogService, IChewsiApi chewsiApi, IAppLoader appLoader)
        {
            _dialogService = dialogService;
            ClaimItems = new ObservableCollection<ClaimItemViewModel>(TestClaims);
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            _chewsiApi = chewsiApi;
            _appLoader = appLoader;

            // Refresh appointments now
            var loadAppointmentsWorker = new BackgroundWorker();
            loadAppointmentsWorker.DoWork += (i, j) =>
            {
                if (AssertDentalApiSet())
                {
                    RefreshAppointments();

                    // Refresh appointments every 3 minutes
                    new DispatcherTimer(new TimeSpan(0, 3, 0), DispatcherPriority.Background, (m, n) => RefreshAppointments(), Dispatcher.CurrentDispatcher);
                }
            };

            // Initialize application
            IsBusy = true;
            // Refresh appointments now
            var initWorker = new BackgroundWorker();
            initWorker.DoWork += (i, j) =>
            {
                // if internal DB file is missing
                if (!_appLoader.IsInitialized())
                {
                    // ask user to choose PMS type and location
                    IsBusy = false;
                    ShowPmsSettings = true;
                    SettingsViewModel = new SettingsViewModel(m =>
                    {
                        // create internal DB file
                        _appLoader.Initialize(m.SelectedType, m.Path, m.Address1, m.Address2, m.Tin);
                        
                        ShowPmsSettings = false;
                        _dentalApi = _appLoader.GetDentalApi();

                        _appLoader.RegisterPlugin(m.SelectedType, m.Address1, m.Address2, m.Tin, _dentalApi.GetVersion());

                        loadAppointmentsWorker.RunWorkerAsync();
                    });
                }
                else
                {
                    _dentalApi = _appLoader.GetDentalApi();
                    // Short version of the UpdatePluginRegistration method, full version will be called only if we allow user to open settings view and change other settings
                    _appLoader.UpdatePluginRegistration(_dentalApi.GetVersion());
                    loadAppointmentsWorker.RunWorkerAsync();
                }
            };
            initWorker.RunWorkerAsync();
        }

        private bool AssertDentalApiSet()
        {
            if (_dentalApi != null)
                return true;

            _dialogService.Show("Practice Management System is not set. Please reinstall application.", "Error");
            return false;
        }

        private void RefreshAppointments()
        {
            IsBusy = true;
            var list = LoadAppointments();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, (Action) (() =>
            {
                ClaimItems.Clear();
                foreach (var item in list)
                {
                    ClaimItems.Add(item);
                }
                IsBusy = false;
            }));
        }

        private IEnumerable<ClaimItemViewModel> LoadAppointments()
        {
            if (!_loadingAppointments)
            {
                _loadingAppointments = true;
                
                try
                {
                    var list = _dentalApi.GetAppointmentsForToday();
                    return list.OrderBy(m => m.IsCompleted)
                        .Select(m => new ClaimItemViewModel
                        {
                            Provider = m.ProviderId,
                            Date = m.Date,
                            Patient = m.PatientName,
                            InsuranceId = m.InsuranceId,
                            PatientId = m.PatientId
                        });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load appointments");
                }
                finally
                {
                    _loadingAppointments = false;
                }
            }
            return new List<ClaimItemViewModel>();
        }

        #region Test data
        public ClaimItemViewModel[] TestClaims
        {
            get { return Enumerable.Range(0, 30).Select(m => GetTestClaim()).ToArray(); }
        }

        readonly Random _random = new Random();
        private SettingsViewModel _settingsViewModel;

        ClaimItemViewModel GetTestClaim()
        {
            return new ClaimItemViewModel
            {
                InsuranceId = _random.Next(10000, 100000).ToString(),
                ChewsiId = _random.Next(100, 1000).ToString(),
                Date = DateTime.Now,
                Provider = "Dr. Jason Hamel",
                Patient = "John Smith #" + _random.Next(100, 1000),
                Status = _random.Next(2) == 0 ? "Payment payment processing...." : "A payment authorization request sent to the subscriber. Please aks them to open the Cheswi app on their mobile device to authorized payment."
            };
        }
        #endregion

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; private set; }
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }
        public DialogService.IDialogService DialogService { get { return _dialogService; } }

        public ClaimItemViewModel SelectedClaim
        {
            get { return _selectedClaim; }
            set
            {
                _selectedClaim = value;
                RaisePropertyChanged(() => SelectedClaim);
            }
        }

        public bool ShowValidationError
        {
            get { return _showValidationError; }
            set
            {
                _showValidationError = value;
                RaisePropertyChanged(() => ShowValidationError);
            }
        }

        public bool ShowPaymentProcessing
        {
            get { return _showPaymentProcessing; }
            set
            {
                _showPaymentProcessing = value;
                RaisePropertyChanged(() => ShowPaymentProcessing);
            }
        }

        public string PaymentProcessingMessage
        {
            get { return _paymentProcessingMessage; }
            set
            {
                _paymentProcessingMessage = value;
                RaisePropertyChanged(() => PaymentProcessingMessage);
            }
        }

        public string ValidationError
        {
            get { return _validationError; }
            set
            {
                _validationError = value;
                RaisePropertyChanged(() => ValidationError);
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                RaisePropertyChanged(() => IsBusy);
            }
        }

        public bool ShowPmsSettings
        {
            get { return _showPmsSettings; }
            set
            {
                _showPmsSettings = value;
                RaisePropertyChanged(() => ShowPmsSettings);
            }
        }

        public SettingsViewModel SettingsViewModel
        {
            get { return _settingsViewModel; }
            set
            {
                _settingsViewModel = value;
                RaisePropertyChanged(() => SettingsViewModel);
            }
        }

        #region SubmitCommand
        public ICommand SubmitCommand
        {
            get { return _submitCommand ?? (_submitCommand = new RelayCommand(OnSubmitCommandExecute)); }
        }

        private void OnSubmitCommandExecute()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, (Action) (() =>
            {
                var provider = _dentalApi.GetProvider(SelectedClaim.Provider);
                if (provider != null)
                {
                    var subscriberInfo = _dentalApi.GetPatientInfo(SelectedClaim.PatientId);
                    if (subscriberInfo != null)
                    {
                        var providerParam = new ProviderInformation
                        {
                            NPI = provider.Npi,
                            RenderingAddress = provider.AddressLine,
                            RenderingCity = provider.City,
                            RenderingState = provider.State,
                            RenderingZip = provider.ZipCode,
                            TIN = provider.Tin
                        };
                        var subscriberParam = new SubscriberInformation
                        {
                            SubscriberDateOfBirth = subscriberInfo.BirthDate.ToString("G"),
                            SubscriberFirstName = subscriberInfo.FirstName,
                            SubscriberLastName = subscriberInfo.LastName
                        };

                        var validationResponse = _chewsiApi.ValidateSubscriberAndProvider(providerParam, subscriberParam);
                        Logger.Debug($"Validated subscriber '{SelectedClaim.PatientId}' and provider '{SelectedClaim.Provider}': '{validationResponse.ValidationPassed}'");
                        if (validationResponse.ValidationPassed)
                        {
                            var procedures = _dentalApi.GetProcedures(SelectedClaim.PatientId);
                            if (procedures.Any())
                            {
                                _chewsiApi.ProcessClaim(providerParam, subscriberParam, procedures.Select(m => 
                                    new ProcedureInformation
                                    {
                                        ProcedureCode = m.Code,
                                        ProcedureCharge = m.Amount.ToString("F"),
                                        DateOfService = m.Date.ToString("G")
                                    }).ToList());
                                Logger.Debug($"Processed claim, found '{procedures.Count}' procedures.");
                            }
                            else
                            {
                                Logger.Error("Cannot find procedure for patient " + SelectedClaim.PatientId);
                            }
                        }
                        else
                        {
                            //DisplayValidationError("Please Validate that the Subscriber's Insurance ID and First Name match the information shown before proceeding. ");
                            DisplayValidationError($"{validationResponse.ProviderValidationMessage} {validationResponse.SubscriberValidationMessage}");                            
                        }
                    }
                    else
                    {
                        Logger.Error("Cannot find patient " + SelectedClaim.PatientId);
                    }
                }
                else
                {
                    Logger.Error("Cannot find provider " + SelectedClaim.Provider);
                }
            }));
        }

        #endregion

        #region DeleteCommand
        public ICommand DeleteCommand
        {
            get { return _deleteCommand ?? (_deleteCommand = new RelayCommand(OnDeleteCommandExecute)); }
        }

        private void OnDeleteCommandExecute()
        {

        }
        #endregion

        #region RefreshAppointmentsCommand
        public ICommand RefreshAppointmentsCommand
        {
            get { return _refreshAppointmentsCommand ?? (_refreshAppointmentsCommand = new RelayCommand(OnRefreshAppointmentsCommandExecute, CanExecuteRefreshAppointmentsCommand)); }
        }

        private bool CanExecuteRefreshAppointmentsCommand()
        {
            return _dentalApi != null && !_loadingAppointments;
        }

        private void OnRefreshAppointmentsCommandExecute()
        {
            LoadAppointments();
        }
        #endregion     
           
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand
        {
            get { return _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute, CanExecuteRefreshDownloadsCommand)); }
        }

        private void OnRefreshDownloadsCommandExecute()
        {

        }

        private bool CanExecuteRefreshDownloadsCommand()
        {
            return _dentalApi != null;
        }
        #endregion

        #region CloseValidationPopupCommand
        public ICommand CloseValidationPopupCommand
        {
            get { return _closeValidationPopupCommand ?? (_closeValidationPopupCommand = new RelayCommand(OnCloseValidationPopupCommandExecute)); }
        }

        private void OnCloseValidationPopupCommandExecute()
        {
            ShowValidationError = false;
        }
        #endregion

        #region CloseProcessingPaymentPopupCommand
        public ICommand CloseProcessingPaymentPopupCommand
        {
            get { return _closeProcessingPaymentPopupCommand ?? (_closeProcessingPaymentPopupCommand = new RelayCommand(OnCloseProcessingPaymentPopupCommandExecute)); }
        }

        private void OnCloseProcessingPaymentPopupCommandExecute()
        {
            ShowPaymentProcessing = false;
        }
        #endregion

        #region DownloadReportCommand
        public ICommand DownloadReportCommand
        {
            get { return _downloadReportCommand ?? (_downloadReportCommand = new RelayCommand(OnDownloadReportCommandExecute)); }
        }

        private void OnDownloadReportCommandExecute()
        {
        }
        #endregion

        #region DownloadCommand
        public ICommand DownloadCommand
        {
            get { return _downloadCommand ?? (_downloadCommand = new RelayCommand(OnDownloadCommandExecute)); }
        }

        private void OnDownloadCommandExecute()
        {
        }
        #endregion

        private void DisplayValidationError(string error)
        {
            ValidationError = error;
            ShowValidationError = true;
        }
    }
}