using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Threading;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.UI.Services
{
    public class AppService : ViewModelBase, IAppService, IDisposable
    {
        private const int RefreshIntervalMs = 10000;
        private const int AppointmentTtlDays = 1;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IChewsiApi _chewsiApi;
        private readonly IRepository _repository;
        private readonly IDialogService _dialogService;
        private IDentalApi _dentalApi;
        private Settings.PMS.Types _pmsType;
        private readonly object _dentalApiInitializationLock = new object();
        private readonly ConcurrentDictionary<string, Provider> _providers;
        private readonly CancellationTokenSource _tokenSource;
        private bool _loadingClaims;
        private readonly object _appointmentsLockObject = new object();
        private readonly object _providerLockObject = new object();

        public AppService(IChewsiApi chewsiApi, IRepository repository, IDialogService dialogService)
        {
            _chewsiApi = chewsiApi;
            _repository = repository;
            _dialogService = dialogService;
            _repository.Initialize();

            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
            _tokenSource = new CancellationTokenSource();
            _providers = new ConcurrentDictionary<string, Provider>();
            Task.Factory.StartNew(StatusLookup, _tokenSource.Token);
        }

        public bool Initialized => _repository.Initialized && DentalApi != null;

        public void SaveSettings(SettingsDto settingsDto)
        {
            string pmsVersionOld = null;
            Settings.PMS.Types pmsTypeOld = Settings.PMS.Types.Dentrix;
            string address1Old = null;
            string address2Old = null;
            string stateOld = null;
            string osOld = null;
            string pluginVersionOld = null;
            string tinOld = null;

            var firstAppRun = !_repository.Initialized;
            if (!firstAppRun)
            {
                // DB already has all the settings, load current values to detect changes below
                pmsVersionOld = _repository.GetSettingValue<string>(Settings.PMS.VersionKey);
                pmsTypeOld = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
                address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
                stateOld = _repository.GetSettingValue<string>(Settings.StateKey);
                osOld = _repository.GetSettingValue<string>(Settings.OsKey);
                pluginVersionOld = _repository.GetSettingValue<string>(Settings.AppVersionKey);
                tinOld = _repository.GetSettingValue<string>(Settings.TIN);
            }

            _repository.SaveSetting(Settings.TIN, settingsDto.Tin);
            _repository.SaveSetting(Settings.PMS.TypeKey, settingsDto.PmsType);
            _repository.SaveSetting(Settings.PMS.PathKey, settingsDto.PmsPath);
            _repository.SaveSetting(Settings.Address1Key, settingsDto.Address1);
            _repository.SaveSetting(Settings.Address2Key, settingsDto.Address2);
            _repository.SaveSetting(Settings.StateKey, settingsDto.State);
            _repository.SaveSetting(Settings.OsKey, Utils.GetOperatingSystemInfo());
            _repository.SaveSetting(Settings.AppVersionKey, Utils.GetPluginVersion());

            _repository.SaveSetting(Settings.UseProxy, settingsDto.UseProxy);
            _repository.SaveSetting(Settings.ProxyAddress, settingsDto.ProxyAddress);
            _repository.SaveSetting(Settings.ProxyPort, settingsDto.ProxyPort);
            _repository.SaveSetting(Settings.ProxyLogin, settingsDto.ProxyLogin);
            _repository.SaveSetting(Settings.ProxyPassword, settingsDto.ProxyPassword);

            // init DentalApi and get version
            _repository.SaveSetting(Settings.PMS.VersionKey, DentalApi.GetVersion());
            
            // DB is empty, settings saved, call RegisterPlugin
            var registeredBefore = !string.IsNullOrEmpty(_repository.GetSettingValue<string>(Settings.MachineIdKey));
            if (!firstAppRun || !registeredBefore)
            {
                var pmsVersion = DentalApi.GetVersion();
                if (registeredBefore)
                {
                    // detect changes and call UpdatePluginRegistration
                    if ((pmsVersion != pmsVersionOld)
                        || pmsTypeOld != settingsDto.PmsType
                        || address1Old != settingsDto.Address1
                        || address2Old != settingsDto.Address2
                        || stateOld != settingsDto.State
                        || osOld != Utils.GetOperatingSystemInfo()
                        || tinOld != settingsDto.Tin
                        || pluginVersionOld != Utils.GetPluginVersion())
                    {
                        Logger.Info("Configuration changes detected. Updating plugin registration...");
                        var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                        InitializeChewsiApi();
                        _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                        Logger.Info("Plugin registration successfuly updated");
                    }                    
                }
                else
                {
                    Logger.Info("Registering plugin...");
                    var machineId = _chewsiApi.RegisterPlugin(new RegisterPluginRequest(settingsDto.Tin, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                    Logger.Info("Machine Id: " + machineId);
                    _repository.SaveSetting(Settings.MachineIdKey, machineId);    
                    InitializeChewsiApi();                                   
                }
            }
        }

        public void InitializeChewsiApi()
        {
            var token = _repository.GetSettingValue<string>(Settings.MachineIdKey);
            var useProxy = _repository.GetSettingValue<bool>(Settings.UseProxy);
            var proxyAddress = _repository.GetSettingValue<string>(Settings.ProxyAddress);
            var proxyPort = _repository.GetSettingValue<int>(Settings.ProxyPort);
            var proxyUserName = _repository.GetSettingValue<string>(Settings.ProxyLogin);
            var proxyPassword = _repository.GetSettingValue<string>(Settings.ProxyPassword);

            _chewsiApi.Initialize(token, useProxy, proxyAddress, proxyPort, proxyUserName, proxyPassword);
        }

        public SettingsDto GetSettings()
        {
            var pmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var pmsPath = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
            var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var useProxy = _repository.GetSettingValue<bool>(Settings.UseProxy);
            var proxyAddress = _repository.GetSettingValue<string>(Settings.ProxyAddress);
            var proxyPassword = _repository.GetSettingValue<string>(Settings.ProxyPassword);
            var proxyPort = _repository.GetSettingValue<int>(Settings.ProxyPort);
            var proxyLogin = _repository.GetSettingValue<string>(Settings.ProxyLogin);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            return new SettingsDto(pmsType, pmsPath, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state);
        }

        public void UpdatePluginRegistration()
        {
            if (_dentalApi != null)
            {
                string pmsVersion = _dentalApi.GetVersion();
                var pmsVersionOld = _repository.GetSettingValue<string>(Settings.PMS.VersionKey);
                var pmsTypeOld = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
                var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
                var osOld = _repository.GetSettingValue<string>(Settings.OsKey);
                var pluginVersionOld = _repository.GetSettingValue<string>(Settings.AppVersionKey);
                if (pmsVersion != pmsVersionOld
                    || osOld != Utils.GetOperatingSystemInfo()
                    || pluginVersionOld != Utils.GetPluginVersion())
                {
                    Logger.Info("Configuration changes detected. Updating plugin registration...");
                    var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                    _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, address1Old,
                        address2Old, pmsTypeOld, pmsVersion));
                    Logger.Info("Plugin registration successfuly updated");
                }
            }
        }

        public ValidateSubscriberAndProviderResponse ValidateClaim(string providerId, string patientId, 
            out ProviderInformation providerInformation, out SubscriberInformation subscriberInformation, out Provider provider)
        {
            try
            {
                _dialogService.ShowLoadingIndicator();
                provider = DentalApi.GetProvider(providerId);
                if (provider != null)
                {
                    var patientInfo = DentalApi.GetPatientInfo(patientId);
                    if (patientInfo != null)
                    {
                        providerInformation = new ProviderInformation
                        {
                            NPI = provider.Npi,
                            TIN = provider.Tin
                        };
                        subscriberInformation = new SubscriberInformation
                        {
                            Id = patientInfo.ChewsiId,
                            SubscriberDateOfBirth = patientInfo.BirthDate,
                            SubscriberFirstName = patientInfo.SubscriberFirstName,
                            SubscriberLastName = patientInfo.SubscriberLastName,
                            PatientLastName = patientInfo.PatientLastName,
                            PatientFirstName = patientInfo.PatientFirstName
                        };
                        var providerAddress = new ProviderAddressInformation
                        {
                            RenderingAddress1 = provider.AddressLine1,
                            RenderingAddress2 = provider.AddressLine2,
                            RenderingCity = provider.City,
                            RenderingState = provider.State,
                            RenderingZip = provider.ZipCode,
                        };
                        try
                        {
                            var validationResponse = _chewsiApi.ValidateSubscriberAndProvider(providerInformation, providerAddress, subscriberInformation);
                            if (validationResponse != null)
                            {
                                Logger.Debug($"Validated subscriber '{patientId}' and provider '{providerId}': '{validationResponse.ValidationPassed}'");
                            }
                            else
                            {
                                Logger.Debug($"Failed to validate subscriber '{patientId}' and provider '{providerId}': invalid server response");
                            }

                            return validationResponse;
                        }
                        catch (NullReferenceException)
                        {
                            _dialogService.Show("Invalid server response. Try again later", "Error");
                        }
                    }
                    else
                    {
                        var msg = "Cannot find patient " + patientId;
                        _dialogService.Show(msg, "Error");
                        Logger.Error(msg);
                    }
                }
                else
                {
                    var msg = "Cannot find provider " + providerId;
                    _dialogService.Show(msg, "Error");
                    Logger.Error(msg);
                }
                providerInformation = null;
                subscriberInformation = null;
                return null;
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public void SubmitClaim(string patientId, ProviderInformation providerInformation, SubscriberInformation subscriberInformation, Provider provider)
        {
            try
            {
                _dialogService.ShowLoadingIndicator();
                var procedures = DentalApi.GetProcedures(patientId);
                if (procedures.Any())
                {
                    _chewsiApi.ProcessClaim(providerInformation, subscriberInformation, procedures.Select(m => new ClaimLine(m.Date, m.Code, m.Amount)).ToList());
                    RequestStatusLookup(provider);

                    Logger.Debug($"Processed claim, found '{procedures.Count}' procedures.");
                }
                else
                {
                    var msg = "Cannot find any procedures for the patient";
                    _dialogService.Show(msg, "Error");
                    Logger.Error(msg + " " + patientId);
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public void UpdateCachedClaim(string chewsiId, DateTime date, AppointmentState state, string statusText)
        {
            var a = _repository.GetAppointmentByChewsiIdAndDate(chewsiId, date);
            if (a != null)
            {
                a.StatusText = statusText;
                a.State = state;

                _repository.UpdateAppointment(a);
            }
        }

        private IDentalApi DentalApi
        {
            get
            {
                var pmsTypeString = _repository.GetSettingValue<string>(Settings.PMS.TypeKey);
                if (pmsTypeString != null)
                {
                    var pmsType = (Settings.PMS.Types)Enum.Parse(typeof(Settings.PMS.Types), pmsTypeString);
                    lock (_dentalApiInitializationLock)
                    {
                        if (_dentalApi == null || pmsType != _pmsType)
                        {
                            // free resources when user changes PMS type
                            if (_dentalApi != null && pmsType != _pmsType)
                            {
                                _dentalApi.Unload();
                            }
                            // create new instance of API on app start and settings changes
                            Logger.Debug("Initializing {0} API", pmsTypeString);
                            switch (pmsType)
                            {
                                case Settings.PMS.Types.Dentrix:
                                    _dentalApi = new DentrixApi(_dialogService);
                                    break;
                                case Settings.PMS.Types.OpenDental:
                                    _dentalApi = new OpenDentalApi.OpenDentalApi(_repository, _dialogService);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            _pmsType = pmsType;
                            CommandManager.InvalidateRequerySuggested();
                        }
                        return _dentalApi;                        
                    }
                }
                Logger.Error("PMS system type is not set");
                return null;
            }
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
                                    // TODO we expect to receive chewsi id in the response, find only by first name for now ( && claimStatus.PostedOnDate == m.Date)
                                    var viewModel = ClaimItems.FirstOrDefault(m => m.PatientName.EndsWith(claimStatus.SubscriberFirstName));
                                    if (viewModel != null)
                                    {
                                        Logger.Debug($"Updating claim status for {claimStatus.PatientFirstName}");

                                        // update cached appointment
                                        var cached = _repository.GetAppointmentByChewsiIdAndDate(viewModel.ChewsiId, viewModel.Date);
                                        if (cached != null)
                                        {
                                            cached.StatusText = claimStatus.MessageToDisplay;

                                            if (claimStatus.Status == ClaimStatusType.SubmitClaim)
                                            {
                                                // will display re-submit button
                                                cached.State = AppointmentState.ValidationError;
                                            }

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
                                        if (claimStatus.Status == ClaimStatusType.SubmitClaim)
                                        {
                                            // will display re-submit button
                                            viewModel.State = AppointmentState.ValidationError;
                                        }

                                        updatedProviderIds.Add(p.Key);
                                    }
                                    else
                                    {
                                        Logger.Debug($"Cannot find view-model to update claim status: {claimStatus.SubscriberFirstName}, {claimStatus.PostedOnDate}; all claims are below");
                                        Logger.Debug(JsonConvert.SerializeObject(ClaimItems.Select(m => new {m.PatientName, m.Date})));
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

        public bool IsLoadingAppointments { get { return _loadingClaims; } }

        private void RaiseIsProcessingPaymentGetter()
        {
            OnStartPaymentStatusLookup?.Invoke();
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
                                    Logger.Debug($"Cached appointment ChewsiId={item.ChewsiId}, IsCompleted={item.IsCompleted}");
                                    _repository.AddAppointment(new Appointment
                                    {
                                        DateTime = item.Date,
                                        State =
                                            item.IsCompleted
                                                ? AppointmentState.TreatmentCompleted
                                                : AppointmentState.TreatmentInProgress,
                                        ChewsiId = item.ChewsiId,
                                        StatusText = item.IsCompleted ? ClaimItemViewModel.StatusMessage.ReadyToSubmit : ClaimItemViewModel.StatusMessage.TreatmentInProgress
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
                                (from c in cached
                                join p in pms on c.ChewsiId equals p.ChewsiId
                                where c.State != AppointmentState.Deleted
                                orderby p.IsCompleted descending
                                select new ClaimItemViewModel(this)
                                {
                                    ProviderId = p.ProviderId,
                                    Date = p.Date,
                                    PatientName = p.PatientName,
                                    PatientId = p.PatientId,
                                    ChewsiId = p.ChewsiId,
                                    State = c.State,
                                    StatusText = c.StatusText
                                }).ToList();
                            result.ForEach(m => Logger.Debug($"Loaded appointment VM ChewsiId={m.ChewsiId}, State={m.State}"));
                            return result;
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

        public event Action OnStartPaymentStatusLookup;
    }
}
