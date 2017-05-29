using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private ICommand _downloadReportCommand;
        private ICommand _downloadCommand;
        private ICommand _refreshDownloadsCommand;
        private ICommand _openSettingsCommandCommand;
        private ICommand _refreshClaimsCommand;
        private ClaimItemViewModel _selectedClaim;
        private DownloadItemViewModel _selectedDownloadItem;

        public MainViewModel(IClientDialogService dialogService, IClientAppService appService)
        {
            DialogService = dialogService;
            AppService = appService;
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            AppService.Initialize();
        }

        #region Properties
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; }

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

        #endregion

        #region Commands       
        #region RefreshAppointmentsCommand
        public ICommand RefreshClaimsCommand => _refreshClaimsCommand ?? (_refreshClaimsCommand = new RelayCommand(OnRefreshClaimsCommandExecute));

        private void OnRefreshClaimsCommandExecute()
        {
            AppService.ReloadClaims(true);
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