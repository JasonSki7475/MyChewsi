using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
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
        private ICommand _downloadReportCommand;
        private ICommand _downloadCommand;
        private ICommand _refreshDownloadsCommand;
        private ICommand _openSettingsCommandCommand;
        private ICommand _refreshAppointmentsCommand;
        private ICommand _paymentProcessingCloseCommand;
        private ClaimItemViewModel _selectedClaim;
        private string _paymentProcessingMessage;
        private readonly IChewsiApi _chewsiApi;
        private readonly IDialogService _dialogService;
        private SettingsViewModel _settingsViewModel;
        private DownloadItemViewModel _selectedDownloadItem;
        private bool _hidePaymentProcessing;

        public MainViewModel(IDialogService dialogService, IChewsiApi chewsiApi, IAppService appService)
        {
            _dialogService = dialogService;
            AppService = appService;
            _chewsiApi = chewsiApi;
        }

        private void StartLoadingAppointments(bool firstRun)
        {
            SettingsViewModel = null;

            AppService.RefreshAppointments(true, !firstRun);

            // Refresh appointments every 3 minutes
            new DispatcherTimer(new TimeSpan(0, 3, 0), DispatcherPriority.Background,
                (m, n) => AppService.RefreshAppointments(true, true), Dispatcher.CurrentDispatcher);
        }

        public void Initialize(bool firstRun)
        {
            AppService.OnStartPaymentStatusLookup += () =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    _hidePaymentProcessing = false;
                    RaisePropertyChanged(() => ShowPaymentProcessing);
                });
            };
            
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();

            _dialogService.ShowLoadingIndicator();
            //TODO
            //AppService.DeleteOldAppointments();

            // Refresh appointments now
            var loadAppointmentsWorker = new BackgroundWorker();
            loadAppointmentsWorker.DoWork += (i, j) =>
            {
                if (!AppService.Initialized && !firstRun)
                {
                    _dialogService.HideLoadingIndicator();
                    Logger.Debug("Cannot load appointments. Settings are empty. Opening settings view");
                    // ask user to choose PMS type and location
                    SettingsViewModel = new SettingsViewModel(AppService, () => StartLoadingAppointments(firstRun), _dialogService);
                }
                else
                {
                    StartLoadingAppointments(firstRun);                   
                }
            };

            // Initialize application
            var initWorker = new BackgroundWorker();
            initWorker.DoWork += (i, j) =>
            {
                if (firstRun)
                {
/*                    if (!AppService.Initialized)
                    {
                        _dialogService.HideLoadingIndicator();
                        Logger.Debug("First run. Settings are empty. Opening settings view");
                        // ask user to choose PMS type and location
                        SettingsViewModel = new SettingsViewModel(AppService, () =>
                        {
                            SettingsViewModel = null;
                            loadAppointmentsWorker.RunWorkerAsync();
                        }, _dialogService);
                    }
                    else*/
                    {
                        loadAppointmentsWorker.RunWorkerAsync();
                        OpenSettingsForReview();
                    }
                }
                else
                {
                    // if internal DB file is missing or it's empty
                    if (!AppService.Initialized)
                    {
                        _dialogService.HideLoadingIndicator();
                        Logger.Debug("Settings are empty. Opening settings view");
                        // ask user to choose PMS type and location
                        SettingsViewModel = new SettingsViewModel(AppService, () =>
                        {
                            SettingsViewModel = null;
                            loadAppointmentsWorker.RunWorkerAsync();
                        }, _dialogService);
                    }    
                    else
                    {
                        AppService.StartPmsIfRequired();
                        AppService.InitializeChewsiApi();
                        AppService.UpdatePluginRegistration();
                        loadAppointmentsWorker.RunWorkerAsync();
                    }                                     
                }
            };
            initWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Display settings view; try to fill Address, State and TIN
        /// </summary>
        private void OpenSettingsForReview()
        {
            Logger.Info("App first run: setup settings");
            Task.Factory.StartNew(() =>
            {
                try
                {
                    // wait till appointments list is ready
                    while (!AppService.AppointmentsLoaded || AppService.IsLoadingAppointments)
                    {
                        Thread.Sleep(200);
                    }

                    // try to find Address, State and TIN in PMS
                    _dialogService.ShowLoadingIndicator();

                    Provider provider = AppService.GetProvider();
                    if (provider != null)
                    {
                        OpenSettingsCommand.Execute(null);
                        DispatcherHelper.CheckBeginInvokeOnUI(() =>
                        {
                            SettingsViewModel.Address1 = provider.AddressLine1;
                            SettingsViewModel.Address2 = provider.AddressLine2;
                            SettingsViewModel.State = provider.State;
                            SettingsViewModel.Tin = provider.Tin;
                            SettingsViewModel.StartLauncher = true;

                            SettingsViewModel.ProxyAddress = "localhost";
                            SettingsViewModel.ProxyPort = 8888;
                        });
                    }
                }
                finally
                {
                    _dialogService.HideLoadingIndicator();
                }
            });
        }

        #region Properties
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }
        public IDialogService DialogService { get { return _dialogService; } }
        public IAppService AppService { private set; get; }

        public bool ShowPaymentProcessing
        {
            get { return !_hidePaymentProcessing && AppService.IsProcessingPayment; }
        }

        public ClaimItemViewModel SelectedClaim
        {
            get { return _selectedClaim; }
            set
            {
                _selectedClaim = value;
                RaisePropertyChanged(() => SelectedClaim);
            }
        }

        public DownloadItemViewModel SelectedDownloadItem
        {
            get { return _selectedDownloadItem; }
            set
            {
                _selectedDownloadItem = value;
                RaisePropertyChanged(() => SelectedDownloadItem);
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
        #region RefreshAppointmentsCommand
        public ICommand RefreshAppointmentsCommand
        {
            get { return _refreshAppointmentsCommand ?? (_refreshAppointmentsCommand = new RelayCommand(OnRefreshAppointmentsCommandExecute, CanExecuteRefreshAppointmentsCommand)); }
        }

        private bool CanExecuteRefreshAppointmentsCommand()
        {
            return !AppService.IsLoadingAppointments && AppService.Initialized;
        }

        private void OnRefreshAppointmentsCommandExecute()
        {
            AppService.RefreshAppointments(true, true);
        }
        #endregion

        #region PaymentProcessingCloseCommand
        public ICommand PaymentProcessingCloseCommand
        {
            get { return _paymentProcessingCloseCommand ?? (_paymentProcessingCloseCommand = new RelayCommand(OnPaymentProcessingCloseCommandExecute)); }
        }
        
        private void OnPaymentProcessingCloseCommandExecute()
        {
            _hidePaymentProcessing = true;
            RaisePropertyChanged(() => ShowPaymentProcessing);
        }
        #endregion     
           
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand
        {
            get { return _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute, CanExecuteRefreshDownloadsCommand)); }
        }

        private void OnRefreshDownloadsCommandExecute()
        {
            var settings = AppService.GetSettings();

            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                _dialogService.ShowLoadingIndicator();
                var list = _chewsiApi.Get835Downloads(new Request835Downloads
                {
                    TIN = settings.Tin,
                    State = settings.State,
                    Address = $"{settings.Address1} {settings.Address2}"
                }).Select(m => new DownloadItemViewModel(m.EDI_835_EDI, m.EDI_835_Report, m.Status, m.PostedDate));

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
            return AppService.Initialized;
        }
        #endregion

        #region OpenSettingsCommand
        public ICommand OpenSettingsCommand
        {
            get { return _openSettingsCommandCommand ?? (_openSettingsCommandCommand = new RelayCommand(OnOpenSettingsCommandCommandExecute)); }
        }

        private void OnOpenSettingsCommandCommandExecute()
        {
            SettingsViewModel = new SettingsViewModel(AppService, () =>
            {
                SettingsViewModel = null;
                AppService.RefreshAppointments(true, true);
            }, _dialogService);
        }
        #endregion

        #region DownloadReportCommand
        public ICommand DownloadReportCommand
        {
            get { return _downloadReportCommand ?? (_downloadReportCommand = new RelayCommand(OnDownloadReportCommandExecute)); }
        }

        private void OnDownloadReportCommandExecute()
        {
            AppService.DownloadFile(SelectedDownloadItem.PdfReportDocumentId, SelectedDownloadItem.PostedDate, true);
        }
        #endregion

        #region DownloadCommand
        public ICommand DownloadCommand
        {
            get { return _downloadCommand ?? (_downloadCommand = new RelayCommand(OnDownloadCommandExecute)); }
        }

        private void OnDownloadCommandExecute()
        {
            AppService.DownloadFile(SelectedDownloadItem.EdiDocumentId, SelectedDownloadItem.PostedDate, false);
        }
        #endregion
        #endregion
    }
}