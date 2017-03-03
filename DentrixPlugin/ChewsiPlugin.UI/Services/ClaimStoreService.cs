using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Threading;
using NLog;

namespace ChewsiPlugin.UI.Services
{
    internal class ClaimStoreService : ViewModelBase, IClaimStoreService, IDisposable
    {
        private const int RefreshIntervalMs = 10000;
        private const int AppointmentTtlDays = 1;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IChewsiApi _chewsiApi;
        private readonly IDialogService _dialogService;
        private readonly IDentalApi _dentalApi;
        private readonly IRepository _repository;
        private readonly ConcurrentDictionary<string, Provider> _providers;
        private readonly CancellationTokenSource _tokenSource;
        private bool _loadingClaims;
        private readonly object _appointmentsLockObject = new object();
        private readonly object _providerLockObject = new object();

        public ClaimStoreService(IChewsiApi chewsiApi, IDialogService dialogService, IDentalApi dentalApi, IRepository repository)
        {
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
            _chewsiApi = chewsiApi;
            _dialogService = dialogService;
            _dentalApi = dentalApi;
            _repository = repository;
            _tokenSource = new CancellationTokenSource();
            _providers = new ConcurrentDictionary<string, Provider>();
            Task.Factory.StartNew(StatusLookup, _tokenSource.Token);
        }

        private void StatusLookup()
        {
            // stop if already canceled
            _tokenSource.Token.ThrowIfCancellationRequested();

            while (true)
            {
                if (_tokenSource.Token.IsCancellationRequested)
                {
                    _tokenSource.Token.ThrowIfCancellationRequested();
                }

                if (_providers.Any())
                {
                    lock (_providerLockObject)
                    {
                        var updatedProviderIds = new List<string>();
                        foreach (var p in _providers)
                        {
                            var request = new ClaimProcessingStatusRequest
                            {
                                TIN = p.Value.Tin,
                                State = p.Value.State,
                                Address = p.Value.AddressLine1
                            };
                            var statusResponse = _chewsiApi.GetClaimProcessingStatus(request);
                            foreach (ClaimStatus claimStatus in statusResponse)
                            {
                                // don't allow to modify appointments list for now
                                lock (_appointmentsLockObject)
                                {
                                    // find in the list, update
                                    var viewModel = ClaimItems.FirstOrDefault(m => claimStatus.PatientName == m.Patient && claimStatus.DateOfService == m.Date);
                                    if (viewModel != null)
                                    {
                                        // update cached appointment
                                        var cached = _repository.GetAppointmentByChewsiIdAndDate(viewModel.ChewsiId, viewModel.Date);
                                        if (cached != null)
                                        {
                                            cached.StatusText = claimStatus.MessageToDisplay;
                                            if (claimStatus.Status == ClaimStatusType.P)
                                            {
                                                cached.State = AppointmentState.PaymentCompleted;
                                            }
                                            _repository.UpdateAppointment(cached);
                                        }

                                        // update view model
                                        viewModel.StatusText = claimStatus.MessageToDisplay;
                                        if (claimStatus.Status == ClaimStatusType.P)
                                        {
                                            viewModel.State = AppointmentState.PaymentCompleted;
                                        }

                                        updatedProviderIds.Add(p.Key);
                                    }
                                }
                            }
                        }
                        // remove updated values from the loop
                        Provider v;
                        updatedProviderIds.ForEach(p => _providers.TryRemove(p, out v));

                        if (updatedProviderIds.Any())
                        {
                            RaiseIsProcessingPaymentGetter();
                        }
                    }
                }
                Thread.Sleep(RefreshIntervalMs);
            }
        }

        public void RequestStatusLookup(Provider provider)
        {
            if (!_providers.ContainsKey(provider.Tin))
            {
                if (_providers.TryAdd(provider.Tin, provider))
                {
                    RaiseIsProcessingPaymentGetter();
                }
            }
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
        }

        public void RefreshAppointments()
        {
            _dialogService.ShowLoadingIndicator();
            var list = LoadAppointments();
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                var existingList = ClaimItems.ToList();

                // add new rows
                foreach (var item in list)
                {
                    bool exists = false;
                    foreach (var viewModel in existingList)
                    {
                        if (item.Equals(viewModel))
                        {
                            exists = true;
                            // update existing view model
                            viewModel.State = item.State;
                            viewModel.StatusText = item.StatusText;
                            break;
                        }
                    }
                    if (!exists)
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

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; private set; }

        public bool IsProcessingPayment
        {
            get { return _providers.Any(); }
        }

        private void RaiseIsProcessingPaymentGetter()
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                RaisePropertyChanged(() => IsProcessingPayment);
            });
        } 

        /// <summary>
        /// Loads appointments from PMS, caches them in local DB
        /// </summary>
        private List<ClaimItemViewModel> LoadAppointments()
        {
            if (!_loadingClaims)
            {
                lock (_appointmentsLockObject)
                {
                    if (!_loadingClaims)
                    {
                        _loadingClaims = true;
                        try
                        {
                            // load from PMS
                            var pms = _dentalApi.GetAppointmentsForToday();

                            // merge with cached in repository items
                            var cached = _repository.GetAppointments();

                            // add new appointments into repository, exist in PMS, not in the DB
                            foreach (var item in pms)
                            {
                                if (cached.All(m => item.ChewsiId != m.ChewsiId))
                                {
                                    _repository.AddAppointment(new Appointment
                                    {
                                        DateTime = item.Date,
                                        State =
                                            item.IsCompleted
                                                ? AppointmentState.TreatmentCompleted
                                                : AppointmentState.TreatmentInProgress,
                                        ChewsiId = item.ChewsiId,
                                        StatusText = ClaimItemViewModel.StatusMessage.ReadyToSubmit
                                    });
                                }
                            }

                            // remove old, exist in the DB, not in PMS
                            var old = (from item in cached
                                where pms.All(m => item.ChewsiId != m.ChewsiId)
                                select item.Id).ToList();
                            if (old.Any())
                            {
                                _repository.BulkDeleteAppointments(old);
                            }

                            // re-read list from repository
                            cached = _repository.GetAppointments();

                            // get result by joining PMS and cached lists
                            var result =
                                from c in cached
                                join p in pms on c.ChewsiId equals p.ChewsiId
                                where c.State != AppointmentState.Deleted
                                orderby p.IsCompleted descending
                                select new ClaimItemViewModel()
                                {
                                    ProviderId = p.ProviderId,
                                    Date = p.Date,
                                    Patient = p.PatientName,
                                    PatientId = p.PatientId,
                                    ChewsiId = p.ChewsiId,
                                    State = c.State,
                                    StatusText = c.StatusText
                                };
                            return result.ToList();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to load appointments");
                        }
                        finally
                        {
                            _loadingClaims = false;
                        }
                    }
                }
            }
            return new List<ClaimItemViewModel>();
        }
        
        public void DeleteAppointment(string chewsiId, DateTime date)
        {
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentByChewsiIdAndDate(chewsiId, date);
                if (existing == null)
                {
                    _repository.AddAppointment(new Appointment
                    {
                        DateTime = DateTime.UtcNow,
                        ChewsiId = chewsiId,
                        State = AppointmentState.Deleted
                    });
                }
                else
                {
                    existing.State = AppointmentState.Deleted;
                    _repository.UpdateAppointment(existing);
                }                
            }
        }

        public void DeleteOldAppointments()
        {
            lock (_appointmentsLockObject)
            {
                var list = _repository.GetAppointments();
                var date = DateTime.Now.Date.AddDays(-AppointmentTtlDays);
                var old = list.Where(m => m.DateTime < date).Select(m => m.Id).ToList();
                if (old.Any())
                {
                    Logger.Info("Deleting {0} old cached appointments", old.Count);
                    _repository.BulkDeleteAppointments(old);
                }                
            }
        }
    }
}