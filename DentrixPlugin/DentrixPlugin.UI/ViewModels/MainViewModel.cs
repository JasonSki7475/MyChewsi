using System.Collections.ObjectModel;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace DentrixPlugin.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ICommand _submitCommand;
        private ICommand _deleteCommand;
        private ICommand _refreshDownloadsCommand;

        public MainViewModel()
        {
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
            HistoryItems = new ObservableCollection<ClaimItemViewModel>();
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
        }

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; private set; }
        public ObservableCollection<ClaimItemViewModel> HistoryItems { get; private set; }
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }

        #region SubmitCommand
        public ICommand SubmitCommand
        {
            get { return _submitCommand ?? (_submitCommand = new RelayCommand(OnSubmitCommandExecute)); }
        }

        private void OnSubmitCommandExecute()
        {

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
        
        #region RefreshDownloadsCommand
        public ICommand RefreshDownloadsCommand
        {
            get { return _refreshDownloadsCommand ?? (_refreshDownloadsCommand = new RelayCommand(OnRefreshDownloadsCommandExecute)); }
        }

        private void OnRefreshDownloadsCommandExecute()
        {

        }
        #endregion 
    }
}