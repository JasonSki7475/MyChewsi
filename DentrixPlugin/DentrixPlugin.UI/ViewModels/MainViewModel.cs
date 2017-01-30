using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DentrixPlugin.Api.DentrixApi;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NLog;

namespace DentrixPlugin.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ICommand _submitCommand;
        private ICommand _deleteCommand;
        private ICommand _refreshDownloadsCommand;
        private ICommand _refreshAppointmentsCommand;
        private ICommand _closeValidationPopupCommand;
        private ClaimItemViewModel _selectedClaim;
        private bool _showValidationError;
        private bool _loadingAppointments;
        private string _validationError;

        public MainViewModel()
        {
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
            HistoryItems = new ObservableCollection<ClaimItemViewModel>(TestHistoricClaims);
            DownloadItems = new ObservableCollection<DownloadItemViewModel>();
            
            // Refresh appointments now
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                RefreshAppointments();

                // Refresh appointments every 3 minutes
                new DispatcherTimer(new TimeSpan(0, 3, 0), DispatcherPriority.Background, (m, n) => RefreshAppointments(), Dispatcher.CurrentDispatcher);
            };
            worker.RunWorkerAsync();
        }

        private void RefreshAppointments()
        {
            var list = LoadAppointments();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, (Action) (() =>
            {
                ClaimItems.Clear();
                foreach (var item in list)
                {
                    ClaimItems.Add(item);
                }
            }));
        }

        private IEnumerable<ClaimItemViewModel> LoadAppointments()
        {
            if (!_loadingAppointments)
            {
                _loadingAppointments = true;

                //TODO Remove
                Thread.Sleep(3000);

                try
                {
                    var list = DentrixApi.GetAppointmentsForToday(DentrixApi.GetAllPatientsInsurance());
                    return list.OrderBy(m => m.IsCompleted)
                        .Select(m => new ClaimItemViewModel
                        {
                            Provider = m.Provider_id,
                            Date = DateTime.Parse(m.Appointment_date),
                            Subscriber = m.Patient_name,
                            InsuranceId = m.Primary_insured_id
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

        public ClaimItemViewModel[] TestClaims
        {
            get { return Enumerable.Range(0, 30).Select(m => GetTestClaim()).ToArray(); }
        }
        public ClaimHistoryItemViewModel[] TestHistoricClaims
        {
            get { return Enumerable.Range(0, 30).Select(m => GetTestHistoricClaim()).ToArray(); }
        }

        readonly Random _random = new Random();
        ClaimItemViewModel GetTestClaim()
        {
            return new ClaimItemViewModel
            {
                InsuranceId = _random.Next(10000, 100000).ToString(),
                Date = DateTime.Now,
                Provider = "Dr. Jason Hamel",
                Subscriber = "John Smith #" + _random.Next(100, 1000)
            };
        }
        ClaimHistoryItemViewModel GetTestHistoricClaim()
        {
            return new ClaimHistoryItemViewModel
            {
                InsuranceId = _random.Next(10000, 100000).ToString(),
                Date = DateTime.Now,
                Provider = "Dr. Jason Hamel",
                Subscriber = "John Smith #" + _random.Next(100, 1000),
                Status = _random.Next(2) == 0 ? "Payment payment processing...." : "A payment authorization request sent to the subscriber. Please aks them to open the Cheswi app on their mobile device to authorized payment."
            };
        }

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; private set; }
        public ObservableCollection<ClaimItemViewModel> HistoryItems { get; private set; }
        public ObservableCollection<DownloadItemViewModel> DownloadItems { get; private set; }

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

        public string ValidationError
        {
            get { return _validationError; }
            set
            {
                _validationError = value;
                RaisePropertyChanged(() => ValidationError);
            }
        }

        #region SubmitCommand
        public ICommand SubmitCommand
        {
            get { return _submitCommand ?? (_submitCommand = new RelayCommand(OnSubmitCommandExecute)); }
        }

        private void OnSubmitCommandExecute()
        {
            var provider = DentrixApi.GetProvider(SelectedClaim.Provider);
            //DisplayValidationError("Please Validate that the Subscriber's Insurance ID and First Name match the information shown before proceeding. ");
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
            return !_loadingAppointments;
        }

        private void OnRefreshAppointmentsCommandExecute()
        {
            LoadAppointments();
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

        private void DisplayValidationError(string error)
        {
            ValidationError = error;
            ShowValidationError = true;
        }
    }
}