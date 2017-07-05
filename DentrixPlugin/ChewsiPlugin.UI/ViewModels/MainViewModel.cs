using System;
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
        private const int ClaimsTabIndex = 0;
        private const int DownloadsTabIndex = 1;
        private const int PaymentsTabIndex = 2;
        private bool _downloadsLoaded;
        private bool _paymentsLoaded;
        private ICommand _downloadReportCommand;
        private ICommand _downloadCommand;
        private ICommand _openSettingsCommandCommand;
        private ICommand _refreshCommand;
        private ICommand _tabChangedCommand;
        private ClaimItemViewModel _selectedClaim;
        private DownloadItemViewModel _selectedDownloadItem;
        private PaymentPlanHistoryViewModel _selectedPayment;
        private int _selectedTab;

        public MainViewModel(IClientDialogService dialogService, IClientAppService appService)
        {
            DialogService = dialogService;
            AppService = appService;
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            PaymentItems = new ObservableCollection<PaymentPlanHistoryViewModel>();
            DesignClaimItems = new ObservableCollection<ClaimItemViewModel>();
            AppService.Initialize();
        }

        #region Properties
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; }
        public ObservableCollection<PaymentPlanHistoryViewModel> PaymentItems { get; }
        public ObservableCollection<ClaimItemViewModel> DesignClaimItems { get; }

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

        public int SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                _selectedTab = value;
                RaisePropertyChanged(() => SelectedTab);
            }
        }

        #endregion

        #region Commands       
        
        #region RefreshAppointmentsCommand
        public ICommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new RelayCommand(OnRefreshCommandExecute));

        private void OnRefreshCommandExecute()
        {
            switch (SelectedTab)
            {
                case ClaimsTabIndex:
                    AppService.ReloadClaims();
                    break;
                case DownloadsTabIndex:
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
                    break;
                case PaymentsTabIndex:
                    worker = new BackgroundWorker();
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
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
        
        #region TabChangedCommand
        public ICommand TabChangedCommand => _tabChangedCommand ?? (_tabChangedCommand = new RelayCommand<SelectionChangedEventArgs>(OnTabChangedCommandExecute));

        private void OnTabChangedCommandExecute(SelectionChangedEventArgs e)
        {
            switch (SelectedTab)
            {
                case ClaimsTabIndex:

                    break;
                case DownloadsTabIndex:
                    if (!_downloadsLoaded && RefreshCommand.CanExecute(null))
                    {
                        _downloadsLoaded = true;
                        RefreshCommand.Execute(null);
                    }
                    break;
                case PaymentsTabIndex:
                    if (!_paymentsLoaded && RefreshCommand.CanExecute(null))
                    {
                        _paymentsLoaded = true;
                        RefreshCommand.Execute(null);
                    }    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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