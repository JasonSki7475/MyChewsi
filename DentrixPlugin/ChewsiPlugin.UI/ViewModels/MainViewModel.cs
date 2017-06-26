using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class MainViewModel : ViewModelBase
    {
        private const int DownloadsTabIndex = 1;
        private const int PaymentsTabIndex = 2;
        private bool _downloadsLoaded;
        private bool _paymentsLoaded;
        private ICommand _downloadReportCommand;
        private ICommand _downloadCommand;
        private ICommand _refreshDownloadsCommand;
        private ICommand _refreshPaymentsCommand;
        private ICommand _openSettingsCommandCommand;
        private ICommand _refreshClaimsCommand;
        private ICommand _tabChangedCommand;
        private ClaimItemViewModel _selectedClaim;
        private DownloadItemViewModel _selectedDownloadItem;
        private PaymentPlanHistoryViewModel _selectedPayment;

        public MainViewModel(IClientDialogService dialogService, IClientAppService appService)
        {
            DialogService = dialogService;
            AppService = appService;
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            PaymentItems = new ObservableCollection<PaymentPlanHistoryViewModel>();
            AppService.Initialize();

            MessengerInstance.Register<PaymentPlanHistoryViewModel>(this, m =>
            {
                // deselect all
                if (SelectedPayment != null && SelectedPayment != m)
                {
                    SelectedPayment.IsSelected = false;
                }
                m.IsSelected = !m.IsSelected;
            });
        }

        #region Properties
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; }
        public ObservableCollection<PaymentPlanHistoryViewModel> PaymentItems { get; }

        public IDialogService DialogService { get; }

        public IClientAppService AppService { get; }
        
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

        public PaymentPlanHistoryViewModel SelectedPayment
        {
            get { return _selectedPayment; }
            set
            {
                _selectedPayment = value;
                RaisePropertyChanged(() => SelectedPayment);
            }
        }

        #endregion

        #region Commands       
        
        #region RefreshAppointmentsCommand
        public ICommand RefreshClaimsCommand => _refreshClaimsCommand ?? (_refreshClaimsCommand = new RelayCommand(OnRefreshClaimsCommandExecute));

        private void OnRefreshClaimsCommandExecute()
        {
            AppService.ReloadClaims();
        }
        #endregion
        
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand => _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute));

        private void OnRefreshDownloadsCommandExecute()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                var list = AppService.GetDownloads();
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    DownloadItems.Clear();
                    foreach (var item in list)
                    {
                        DownloadItems.Add(item);
                    }
                });
            };
            worker.RunWorkerAsync();
        }
        #endregion

        #region TabChangedCommand
        public ICommand TabChangedCommand => _tabChangedCommand ?? (_tabChangedCommand = new RelayCommand<SelectionChangedEventArgs>(OnTabChangedCommandExecute));

        private void OnTabChangedCommandExecute(SelectionChangedEventArgs e)
        {
            var a = e.Source as TabControl;
            if (a != null)
            {
                if (a.SelectedIndex == DownloadsTabIndex)
                {
                    if (!_downloadsLoaded && RefreshDownloadsCommand.CanExecute(null))
                    {
                        _downloadsLoaded = true;
                        RefreshDownloadsCommand.Execute(null);
                    }
                }
                if (a.SelectedIndex == PaymentsTabIndex)
                {
                    if (!_paymentsLoaded && RefreshPaymentsCommand.CanExecute(null))
                    {
                        _paymentsLoaded = true;
                        RefreshPaymentsCommand.Execute(null);
                    }                    
                }
            }
        }
        #endregion

        #region RefreshPaymentsCommand
        public ICommand RefreshPaymentsCommand => _refreshPaymentsCommand ?? (_refreshPaymentsCommand = new RelayCommand(OnRefreshPaymentsCommandExecute));

        private void OnRefreshPaymentsCommandExecute()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                var list = AppService.GetPayments();
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    PaymentItems.Clear();
                    foreach (var item in list)
                    {
                        PaymentItems.Add(item);
                    }
                });
            };
            worker.RunWorkerAsync();
        }
        #endregion

        #region OpenSettingsCommand
        public ICommand OpenSettingsCommand => _openSettingsCommandCommand ?? (_openSettingsCommandCommand = new RelayCommand(OnOpenSettingsCommandCommandExecute));

        private void OnOpenSettingsCommandCommandExecute()
        {
            AppService.OpenSettings();
        }
        #endregion

        #region DownloadReportCommand
        public ICommand DownloadReportCommand => _downloadReportCommand ?? (_downloadReportCommand = new RelayCommand(OnDownloadReportCommandExecute));

        private void OnDownloadReportCommandExecute()
        {
            AppService.DownloadFile(SelectedDownloadItem.PdfReportDocumentId, SelectedDownloadItem.PostedDate, true);
        }
        #endregion

        #region DownloadCommand
        public ICommand DownloadCommand => _downloadCommand ?? (_downloadCommand = new RelayCommand(OnDownloadCommandExecute));

        private void OnDownloadCommandExecute()
        {
            AppService.DownloadFile(SelectedDownloadItem.EdiDocumentId, SelectedDownloadItem.PostedDate, false);
        }
        #endregion
        #endregion
    }
}