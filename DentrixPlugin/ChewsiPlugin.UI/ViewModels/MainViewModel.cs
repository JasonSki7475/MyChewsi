using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using Microsoft.Win32;
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
        private ClaimItemViewModel _selectedClaim;
        private string _paymentProcessingMessage;
        private readonly IChewsiApi _chewsiApi;
        private readonly IAppService _appService;

        private readonly IDialogService _dialogService;
        private SettingsViewModel _settingsViewModel;
        private DownloadItemViewModel _selectedDownloadItem;

        public MainViewModel(IDialogService dialogService, IChewsiApi chewsiApi, IAppService appService,
            IClaimStoreService claimStoreService)
        {
            _dialogService = dialogService;
            _appService = appService;
            _chewsiApi = chewsiApi;
            ClaimStoreService = claimStoreService;
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();

            _dialogService.ShowLoadingIndicator();
            ClaimStoreService.DeleteOldAppointments();

            //Thread.Sleep(10000);

            // Refresh appointments now
            var loadAppointmentsWorker = new BackgroundWorker();
            loadAppointmentsWorker.DoWork += (i, j) =>
            {
                ClaimStoreService.RefreshAppointments();

                // Refresh appointments every 3 minutes
                new DispatcherTimer(new TimeSpan(0, 3, 0), DispatcherPriority.Background,
                    (m, n) => ClaimStoreService.RefreshAppointments(), Dispatcher.CurrentDispatcher);
            };

            // Initialize application
            var initWorker = new BackgroundWorker();
            initWorker.DoWork += (i, j) =>
            {

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

        #region Properties
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }
        public IDialogService DialogService { get { return _dialogService; } }
        public IClaimStoreService ClaimStoreService { get; private set; }

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
            return !_loadingAppointments && _appService.Initialized;
        }

        private void OnRefreshAppointmentsCommandExecute()
        {
            ClaimStoreService.RefreshAppointments();
        }
        #endregion     
           
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand
        {
            get { return _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute, CanExecuteRefreshDownloadsCommand)); }
        }

        private void OnRefreshDownloadsCommandExecute()
        {
            var settings = _appService.GetSettings();

            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                _dialogService.ShowLoadingIndicator();
                var list = _chewsiApi.Get835Downloads(new Request835Downloads
                {
                    TIN = settings.Tin,
                    State = settings.State,
                    Address = $"{settings.Address1} {settings.Address2}"
                }).Select(m => new DownloadItemViewModel(m.File_835_EDI_url, m.File_835_Report_url, m.Payee_ID, DateTime.Parse(m.PostedOnDate)));

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
                ClaimStoreService.RefreshAppointments();
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
            DownloadFile(SelectedDownloadItem.ReportUrl, "Save report file");
        }
        #endregion

        #region DownloadCommand
        public ICommand DownloadCommand
        {
            get { return _downloadCommand ?? (_downloadCommand = new RelayCommand(OnDownloadCommandExecute)); }
        }

        private void OnDownloadCommandExecute()
        {
            DownloadFile(SelectedDownloadItem.Url, "Save 835 file");
        }

        private void DownloadFile(string uri, string dialogTitle)
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                var stream = (MemoryStream) _chewsiApi.DownloadFile(new DownoadFileRequest
                {
                    FileType = DownoadFileType.Pdf,
                    url = uri,
                    payee_ID = SelectedDownloadItem.PayeeId,
                    postedOnDate = SelectedDownloadItem.PostedOnDate.ToString("d")
                });
                var dialog = new SaveFileDialog
                {
                    FileName = Path.GetFileName(uri),
                    Title = dialogTitle,
                    Filter = "PDF file (*.pdf)"
                };
                if (dialog.ShowDialog() == true)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    using (FileStream file = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write))
                    {
                        stream.WriteTo(file);
                        stream.Close();
                    }
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        #endregion
        #endregion
    }
}