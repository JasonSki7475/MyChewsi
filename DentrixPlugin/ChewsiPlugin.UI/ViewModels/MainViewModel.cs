using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
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
        private ICommand _openSettingsCommandCommand;
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
        private readonly IAppService _appService;
        private readonly IDialogService _dialogService;
        private SettingsViewModel _settingsViewModel;

        public MainViewModel(IDialogService dialogService, IChewsiApi chewsiApi, IAppService appService)
        {
            _dialogService = dialogService;
            ClaimItems = new ObservableCollection<ClaimItemViewModel>(TestClaims);
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            _chewsiApi = chewsiApi;
            _appService = appService;

            //Thread.Sleep(10000);

            // Refresh appointments now
            var loadAppointmentsWorker = new BackgroundWorker();
            loadAppointmentsWorker.DoWork += (i, j) =>
            {
                RefreshAppointments();

                // Refresh appointments every 3 minutes
                new DispatcherTimer(new TimeSpan(0, 3, 0), DispatcherPriority.Background, (m, n) => RefreshAppointments(), Dispatcher.CurrentDispatcher);
            };

            // Initialize application
            var initWorker = new BackgroundWorker();
            initWorker.DoWork += (i, j) =>
            {
                _dialogService.ShowLoadingIndicator();
                // if internal DB file is missing or it's empty
                if (!_appService.Initialized)
                {
                    _dialogService.HideLoadingIndicator();
                    Logger.Debug("Settings are empty. Opening settings view");
                    // ask user to choose PMS type and location
                    SettingsViewModel = new SettingsViewModel(_appService, () =>
                    {
                        SettingsViewModel = null;
                        loadAppointmentsWorker.RunWorkerAsync();
                    }, _dialogService);
                }
                else
                {
                    _appService.UpdatePluginRegistration();
                    loadAppointmentsWorker.RunWorkerAsync();
                }
                _appService.InitializeChewsiApi();
            };
            initWorker.RunWorkerAsync();
        }
        
        private void RefreshAppointments()
        {
            _dialogService.ShowLoadingIndicator();
            var list = LoadAppointments().ToList();
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                var existingList = ClaimItems.ToList();

                // add new rows
                foreach (var item in list)
                {
                    if (existingList.All(m => !item.Equals(m)))
                    {
                        existingList.Add(item);
                    }
                }
                ClaimItems.Clear();

                // remove old
                foreach (var item in existingList)
                {
                    if (list.Any(m => item.Equals(m)))
                    {
                        ClaimItems.Add(item);
                    }
                }
                _dialogService.HideLoadingIndicator();
            });
        }
        
        private void DisplayValidationError(string error)
        {
            ValidationError = error;
            ShowValidationError = true;
        }

        private IEnumerable<ClaimItemViewModel> LoadAppointments()
        {
            if (!_loadingAppointments)
            {
                _loadingAppointments = true;
                
                try
                {
                    var list = _appService.DentalApi.GetAppointmentsForToday();
                    return list.OrderBy(m => m.IsCompleted)
                        .Select(m => new ClaimItemViewModel
                        {
                            Provider = m.ProviderId,
                            Date = m.Date,
                            Patient = m.PatientName,
                            //InsuranceId = m.InsuranceId,
                            PatientId = m.PatientId,
                            ChewsiId = m.PrimaryInsuredId
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
        
        ClaimItemViewModel GetTestClaim()
        {
            return new ClaimItemViewModel
            {
                ChewsiId = _random.Next(100, 1000).ToString(),
                Date = DateTime.Now,
                Provider = "Dr. Jason Hamel",
                Patient = "John Smith #" + _random.Next(100, 1000),
                Status = _random.Next(2) == 0 ? "Payment payment processing...." : "A payment authorization request sent to the subscriber. Please aks them to open the Cheswi app on their mobile device to authorized payment."
            };
        }
        #endregion

        #region Properties
        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; private set; }
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }
        public IDialogService DialogService { get { return _dialogService; } }

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

        public SettingsViewModel SettingsViewModel
        {
            get { return _settingsViewModel; }
            set
            {
                _settingsViewModel = value;
                RaisePropertyChanged(() => SettingsViewModel);
            }
        }
        #endregion

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
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                var provider = _appService.DentalApi.GetProvider(SelectedClaim.Provider);
                if (provider != null)
                {
                    var subscriberInfo = _appService.DentalApi.GetPatientInfo(SelectedClaim.PatientId);
                    if (subscriberInfo != null)
                    {
                        var providerParam = new ProviderInformation
                        {
                            NPI = provider.Npi,
                            TIN = provider.Tin
                        };
                        var subscriberParam = new SubscriberInformation
                        {
                            Id = subscriberInfo.PrimaryInsuredId,
                            SubscriberDateOfBirth = subscriberInfo.BirthDate,
                            SubscriberFirstName = subscriberInfo.FirstName,
                            SubscriberLastName = subscriberInfo.LastName
                        };
                        var providerAddress = new ProviderAddressInformation
                        {
                            RenderingAddress = provider.AddressLine,
                            RenderingCity = provider.City,
                            RenderingState = provider.State,
                            RenderingZip = provider.ZipCode,
                        };
                        try
                        {

                            var validationResponse = _chewsiApi.ValidateSubscriberAndProvider(providerParam,
                                providerAddress, subscriberParam);
                            Logger.Debug(
                                $"Validated subscriber '{SelectedClaim.PatientId}' and provider '{SelectedClaim.Provider}': '{validationResponse.ValidationPassed}'");
                            if (validationResponse.ValidationPassed)
                            {
                                var procedures = _appService.DentalApi.GetProcedures(SelectedClaim.PatientId);
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
                                DisplayValidationError(
                                    $"{validationResponse.ProviderValidationMessage} {validationResponse.SubscriberValidationMessage}");
                            }
                            SelectedClaim.Status =
                                $"{validationResponse.ProviderValidationMessage}{Environment.NewLine}{validationResponse.SubscriberValidationMessage}";

                        }
                        catch (NullReferenceException)
                        {
                            _dialogService.Show("Invalid server response. Try again later", "Error");
                        }
                    }
                    else
                    {
                        var msg = "Cannot find patient " + SelectedClaim.PatientId;
                        _dialogService.Show(msg, "Error");
                        Logger.Error(msg);
                    }
                }
                else
                {
                    var msg = "Cannot find provider " + SelectedClaim.Provider;
                    _dialogService.Show(msg, "Error");
                    Logger.Error(msg);
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

        }
        #endregion

        #region RefreshAppointmentsCommand
        public ICommand RefreshAppointmentsCommand
        {
            get { return _refreshAppointmentsCommand ?? (_refreshAppointmentsCommand = new RelayCommand(OnRefreshAppointmentsCommandExecute, CanExecuteRefreshAppointmentsCommand)); }
        }

        private bool CanExecuteRefreshAppointmentsCommand()
        {
            return !_loadingAppointments && _appService.Initialized;
        }

        private void OnRefreshAppointmentsCommandExecute()
        {
            RefreshAppointments();
        }
        #endregion     
           
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand
        {
            get { return _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute, CanExecuteRefreshDownloadsCommand)); }
        }

        private void OnRefreshDownloadsCommandExecute()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                _dialogService.ShowLoadingIndicator();
                var list = _chewsiApi.Get835Downloads(new Request835Downloads
                {//TODO
                    TIN = "454591743",
                    State = "MA",
                    Address = ""
                }).Select(m => new DownloadItemViewModel
                {
                    Status = m.File_835_EDI_url,
                    Date = DateTime.Parse(m.PostedOnDate)
                });

                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    DownloadItems.Clear();
                    foreach (var item in list)
                    {
                        DownloadItems.Add(item);
                    }
                    _dialogService.HideLoadingIndicator();
                });
            };
            worker.RunWorkerAsync();
        }

        private bool CanExecuteRefreshDownloadsCommand()
        {
            return _appService.Initialized;
        }
        #endregion

        #region OpenSettingsCommand
        public ICommand OpenSettingsCommand
        {
            get { return _openSettingsCommandCommand ?? (_openSettingsCommandCommand = new RelayCommand(OnOpenSettingsCommandCommandExecute)); }
        }

        private void OnOpenSettingsCommandCommandExecute()
        {
            SettingsViewModel = new SettingsViewModel(_appService, () =>
            {
                SettingsViewModel = null;
                RefreshAppointments();
            }, _dialogService);
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
        #endregion
    }
}