using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using Microsoft.Win32;
using NLog;
using Appointment = ChewsiPlugin.Api.Repository.Appointment;

namespace ChewsiPlugin.Service
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    internal class ServerAppService : IServerAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _appointmentsLockObject = new object();
        private const string ChewsiLauncherRegistryKey = "Chewsi Launcher";
        private const string ChewsiLauncherExecutableName = "ChewsiPlugin.Launcher.exe";
        private const int AppointmentTtlDays = 1;
        private readonly List<ClaimDto> _serviceClaims = new List<ClaimDto>();
        private readonly IRepository _repository;
        private readonly IChewsiApi _chewsiApi;
        private bool _loadingAppointments;
        private bool _appointmentsLoaded;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationTokenSource _loadTokenSource;
        private readonly List<StatusLookupWaitItem> _statusWaitList = new List<StatusLookupWaitItem>();
        private const int StatusLookupTimeoutSeconds = 60;
        private readonly object _statusWaitListLock = new object();
        private const int StatusRefreshIntervalMs = 10000;

        private IDentalApi _dentalApi;
        private Settings.PMS.Types _pmsType;
        private readonly object _dentalApiInitializationLock = new object();
        private readonly IDentalApiFactoryService _dentalApiFactoryService;
        private readonly DialogServiceBroadcaster _dialogServiceBroadcaster;
        private const int LoadIntervalMs = 180000;
        private readonly List<ClaimDto> _claimItems = new List<ClaimDto>();

        public ServerAppService(IRepository repository, IChewsiApi chewsiApi, IDentalApiFactoryService dentalApiFactoryService, DialogServiceBroadcaster dialogServiceBroadcaster)
        {
            _repository = repository;
            _repository.Initialize();

            _dentalApiFactoryService = dentalApiFactoryService;
            _dialogServiceBroadcaster = dialogServiceBroadcaster;
            _dentalApi = GetDentalApi();
            
            _chewsiApi = chewsiApi;
            _tokenSource = new CancellationTokenSource();
            _loadTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(StatusLookup, _tokenSource.Token);
        }

        public bool InitClient()
        {
            if (_dentalApi == null)
                return false;

            var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();
            _dialogServiceBroadcaster.AddClient(OperationContext.Current.Channel.SessionId, callback);
            return true;
        }

        public void DisconnectClient()
        {
            OperationContext.Current.Channel.Faulted += ChannelOnFaulted;
            _dialogServiceBroadcaster.RemoveClient(OperationContext.Current.Channel.SessionId);
        }

        private void ChannelOnFaulted(object sender, EventArgs eventArgs)
        {
            var ch = sender as IContextChannel;
            _dialogServiceBroadcaster.RemoveClient(ch.SessionId);
        }

        private IDentalApi GetDentalApi()
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
                        _dentalApi = _dentalApiFactoryService.GetDentalApi(pmsType);
                        _pmsType = pmsType;
                    }
                    return _dentalApi;
                }
            }
            Logger.Error("PMS system type is not set");
            return null;
        }
        
        public bool Initialized => _repository.Initialized && GetDentalApi() != null;

        public bool IsLoadingAppointments => _loadingAppointments;

        private ValidateSubscriberAndProviderResponse ValidateClaim(string providerId, string patientId,
            out ProviderInformation providerInformation, out SubscriberInformation subscriberInformation,
            out Provider provider)
        {
            provider = GetDentalApi().GetProvider(providerId);
            if (provider != null)
            {
                var patientInfo = GetDentalApi().GetPatientInfo(patientId);
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
                else
                {
                    var msg = "Cannot find patient " + patientId;
                    _dialogServiceBroadcaster.Show(msg, "Error");
                    Logger.Error(msg);
                }
            }
            else
            {
                var msg = "Cannot find provider " + providerId;
                _dialogServiceBroadcaster.Show(msg, "Error");
                Logger.Error(msg);
            }
            providerInformation = null;
            subscriberInformation = null;
            return null;
        }

        private void SubmitClaim(string appointmentId, DateTime appointmentDate, string patientId, ProviderInformation providerInformation, 
            SubscriberInformation subscriberInformation, DateTime pmsModifiedDate)
        {
            var procedures = GetDentalApi().GetProcedures(patientId, appointmentId, appointmentDate);
            if (procedures.Any())
            {
                // Since we cannot match claim number to appointment (ProcessClaim response is empty; claim number was expected to be there, but API developers cannot implement it so)
                // We have to save current claim numbers for providerId+chewsiId+date+claimnumbers and wait till one more status appear for this combination in status lookup response; or till timeout
                lock (_statusWaitListLock)
                {
                    _statusWaitList.Add(new StatusLookupWaitItem(subscriberInformation.Id, providerInformation.Id, appointmentDate.Date,
                        _claimItems.Where(m => m.IsClaimStatus
                                              && m.ProviderId == providerInformation.Id
                                              && m.ChewsiId == subscriberInformation.Id
                                              && m.Date.Date == appointmentDate.Date)
                            .Select(m => m.ClaimNumber).ToList()));
                }
                _chewsiApi.ProcessClaim(providerInformation, subscriberInformation, 
                    procedures.Select(m => new ClaimLine(m.Date, m.Code, m.Amount)).ToList(),
                    pmsModifiedDate);
                //DispatcherHelper.RunAsync(() =>
                //{
                    SetAppointmentState(appointmentId, AppointmentState.ValidationCompletedAndClaimSubmitted);
                    RefreshAppointments(false, false);
                    Logger.Debug($"Processed claim, found '{procedures.Count}' procedures.");
                //});
            }
            else
            {
                var msg = "Cannot find any procedures for the patient";
                _dialogServiceBroadcaster.Show(msg, "Error");
                Logger.Error(msg + " " + patientId);
            }
        }

        private List<ClaimProcessingStatusResponse> GetClaimStatuses(IEnumerable<string> providerIds)
        {
            var result = new List<ClaimProcessingStatusResponse>();
            Parallel.ForEach(providerIds, providerId =>
            {
                var provider = GetDentalApi().GetProvider(providerId);
                var request = new ClaimProcessingStatusRequest
                {
                    TIN = provider.Tin,
                    State = provider.State,
                    Address = provider.AddressLine1
                };
                var statusResponse = _chewsiApi.GetClaimProcessingStatus(request);
                if (statusResponse != null)
                {
                    statusResponse.ForEach(m => m.ProviderId = providerId);
                    result.Add(statusResponse);
                }
                /*else
                {
                    _dialogService.Show("Error getting claim status. Bad server response.");
                }*/
            });
            return result;
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
                bool pendingLookup = false;

                lock (_statusWaitListLock)
                {
                    if (_statusWaitList.Any())
                    {
                        // check for timeouts; delete expired
                        for (int i = _statusWaitList.Count - 1; i >= 0; i--)
                        {
                            if ((DateTime.UtcNow - _statusWaitList[i].Created).TotalSeconds > StatusLookupTimeoutSeconds)
                            {
                                _statusWaitList.RemoveAt(i);
                            }
                            else
                            {
                                pendingLookup = true;
                            }
                        }
                    }
                }
                if (pendingLookup)
                {
                    RefreshAppointments(false, true);
                }
                Thread.Sleep(StatusRefreshIntervalMs);
            }
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
            _loadTokenSource.Cancel();
        }

        public void RefreshAppointments(bool loadFromPms, bool loadFromService)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                if (!_loadingAppointments)
                {
                    lock (_appointmentsLockObject)
                    {
                        if (!_loadingAppointments)
                        {
                            _loadingAppointments = true;
                            _dialogServiceBroadcaster.ShowLoadingIndicator("Loading...");

                            try
                            {
                                var list = LoadAppointments(loadFromPms, loadFromService);

                                //DispatcherHelper.RunAsync(() =>
                                //{
                                    _claimItems.Clear();
                                    foreach (var item in list)
                                    {
                                        _claimItems.Add(item);
                                    }

                                    _dialogServiceBroadcaster.HideLoadingIndicator();
                                    _loadingAppointments = false;
                                    _appointmentsLoaded = true;
                                //});
                            }
                            catch (Exception)
                            {
                                _loadingAppointments = false;
                                _appointmentsLoaded = true;
                            }
                        }
                    }
                }
            };
            worker.RunWorkerAsync();
        }

        private void StartLoadingAppointments(bool firstRun)
        {
            RefreshAppointments(true, !firstRun);
            
            Task.Factory.StartNew(LoadAppointmentsLoop, _loadTokenSource.Token);
        }

        private void LoadAppointmentsLoop()
        {
            // stop if already canceled
            _tokenSource.Token.ThrowIfCancellationRequested();

            while (true)
            {
                if (_tokenSource.Token.IsCancellationRequested)
                {
                    _tokenSource.Token.ThrowIfCancellationRequested();
                }

                RefreshAppointments(true, true);
                
                // Refresh appointments every 3 minutes
                Thread.Sleep(LoadIntervalMs);
            }
        }

        private void SetAppointmentState(string id, AppointmentState? state = null, string statusText = null)
        {
            bool updated = false;
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    if (state.HasValue && existing.State != state.Value)
                    {
                        existing.State = state.Value;
                        updated = true;
                    }
                    if (statusText != null && existing.StatusText != statusText)
                    {
                        existing.StatusText = statusText;
                        updated = true;
                    }
                    _repository.UpdateAppointment(existing);
                }
            }
            if (updated)
            {
                RefreshAppointments(false, false);
            }
        }

        private static class StatusMessage
        {
            public const string PaymentProcessing = "Payment processing...";
            public const string PaymentProcessingError = "Payment processing failed";
            public const string ReadyToSubmit = "Please submit this claim..";
        }

        public void ValidateAndSubmitClaim(string appointmentId, DateTime date, string providerId, string patientId, DateTime pmsModifiedDate)
        {
            ProviderInformation providerInformation;
            SubscriberInformation subscriberInformation;
            Provider provider;

            SetAppointmentState(appointmentId, AppointmentState.TreatmentCompleted, StatusMessage.PaymentProcessing);
            var validationResponse = ValidateClaim(providerId, patientId, out providerInformation, out subscriberInformation, out provider);
            if (validationResponse != null)
            {
                if (validationResponse.ValidationPassed)
                {
                    providerInformation.Id = validationResponse.ProviderID;
                    providerInformation.OfficeNbr = validationResponse.OfficeNumber;

                    SubmitClaim(appointmentId, date, patientId, providerInformation, subscriberInformation, pmsModifiedDate);
                }
                else
                {
                    #region Format status

                    var statusText = "";
                    if (!string.IsNullOrEmpty(validationResponse.ProviderValidationMessage))
                    {
                        statusText += validationResponse.ProviderValidationMessage;
                    }
                    if (!string.IsNullOrEmpty(validationResponse.SubscriberValidationMessage))
                    {
                        if (!string.IsNullOrEmpty(statusText))
                        {
                            statusText += Environment.NewLine;
                        }
                        statusText += validationResponse.SubscriberValidationMessage;
                    }

                    #endregion

                    SetAppointmentState(appointmentId, AppointmentState.ValidationError, statusText);
                }
            }
            else
            {
                SetAppointmentState(appointmentId, AppointmentState.ValidationError, StatusMessage.PaymentProcessingError);
            }
        }

        public SettingsDto GetSettings()
        {
            var pmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var pmsPath = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
            var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var useProxy = _repository.GetSettingValue<bool>(Settings.UseProxy);
            var startPms = _repository.GetSettingValue<bool>(Settings.StartPms);
            var proxyAddress = _repository.GetSettingValue<string>(Settings.ProxyAddress);
            var proxyPassword = _repository.GetSettingValue<string>(Settings.ProxyPassword);
            var proxyPort = _repository.GetSettingValue<int>(Settings.ProxyPort);
            var proxyLogin = _repository.GetSettingValue<string>(Settings.ProxyLogin);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
            var isClient = _repository.GetSettingValue<bool>(Settings.IsClient);
            return new SettingsDto(pmsType, pmsPath, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state, 
                startPms, GetLauncherStartup(), machineId, isClient);
        }

        private void StartPmsIfRequired()
        {
            var type = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var start = _repository.GetSettingValue<bool>(Settings.StartPms);
            if (start || type == Settings.PMS.Types.Eaglesoft)
            {
                GetDentalApi().StartPms();
            }
        }

        private void SetLauncherStartup(bool enabled)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    if (enabled)
                    {
                        if (key.GetValue(ChewsiLauncherRegistryKey) == null)
                        {
                            key.SetValue(ChewsiLauncherRegistryKey, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ChewsiLauncherExecutableName));
                        }
                    }
                    else
                    {
                        key.DeleteValue(ChewsiLauncherRegistryKey, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Cannot save launcher startup settings in registry");
            }
        }

        private void UpdatePluginRegistration()
        {
            if (GetDentalApi() != null)
            {
                string pmsVersion = GetDentalApi().GetVersion();
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
                    Logger.Info("Plugin registration successfully updated");
                }
            }
        }

        public void SaveSettings(SettingsDto settingsDto)
        {
            try
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

                //_repository.SaveSetting(Settings.IsClient, settingsDto.IsClient);
                _repository.SaveSetting(Settings.StartPms, settingsDto.StartPms);
                _repository.SaveSetting(Settings.UseProxy, settingsDto.UseProxy);
                _repository.SaveSetting(Settings.ProxyAddress, settingsDto.ProxyAddress);
                _repository.SaveSetting(Settings.ProxyPort, settingsDto.ProxyPort);
                _repository.SaveSetting(Settings.ProxyLogin, settingsDto.ProxyLogin);
                _repository.SaveSetting(Settings.ProxyPassword, settingsDto.ProxyPassword);

                // init DentalApi and get version
                _repository.SaveSetting(Settings.PMS.VersionKey, GetDentalApi().GetVersion());

                // start or kill launcher
                var currentLaunchSetting = GetLauncherStartup();
                if (currentLaunchSetting ^ settingsDto.StartLauncher)
                {
                    if (settingsDto.StartLauncher)
                    {
                        StartLauncher();
                    }
                    else
                    {
                        KillLauncher();
                    }
                }
                SetLauncherStartup(settingsDto.StartLauncher);

                // DB is empty, settings saved, call RegisterPlugin
                var registeredBefore = !string.IsNullOrEmpty(_repository.GetSettingValue<string>(Settings.MachineIdKey));
                if (!firstAppRun || !registeredBefore)
                {
                    var pmsVersion = GetDentalApi().GetVersion();
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
                            Logger.Info("Plugin registration successfully updated");
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
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving settings");
                _dialogServiceBroadcaster.Show("Some settings are incorrect or empty. Please enter again.", "Error");
            }
        }

        private void StartLauncher()
        {
            var currentSessionId = Process.GetCurrentProcess().SessionId;
            Process[] runningProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ChewsiLauncherExecutableName));
            if (runningProcesses.All(m => m.SessionId != currentSessionId))
            {
                try
                {
                    Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ChewsiLauncherExecutableName));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to start launcher");
                }
            }
        }

        private void KillLauncher()
        {
            var currentSessionId = Process.GetCurrentProcess().SessionId;
            Process process = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ChewsiLauncherExecutableName)).FirstOrDefault(m => m.SessionId == currentSessionId);
            if (process != null)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to kill launcher");
                }
            }
        }

        private bool GetLauncherStartup()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                if (key != null)
                {
                    return key.GetValue(ChewsiLauncherRegistryKey) != null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Cannot load launcher startup settings from registry");
            }
            return false;
        }

        private void DeleteOldAppointments()
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

        private void InitializeChewsiApi()
        {
            var s = GetSettings();
            _chewsiApi.Initialize(s.MachineId, s.UseProxy, s.ProxyAddress, s.ProxyPort, s.ProxyLogin, s.ProxyPassword);
        }

        private void loadAppointmentsWorker(bool firstRun)
        {
            if (!Initialized && !firstRun)
            {
                _dialogServiceBroadcaster.HideLoadingIndicator();
                Logger.Debug("Cannot load appointments. Settings are empty. Opening settings view");
                // ask user to choose PMS type and location
                //_settingsViewModel.Show(() => StartLoadingAppointments(firstRun));
            }
            else
            {
                StartLoadingAppointments(firstRun);
            }
        }

        public void Initialize(bool firstRun)
        {
            DeleteOldAppointments();

            if (firstRun)
            {
                loadAppointmentsWorker(firstRun);
                //OpenSettingsForReview();
            }
            else
            {
                // if internal DB file is missing or it's empty
                if (!Initialized)
                {
                    _dialogServiceBroadcaster.HideLoadingIndicator();
                    Logger.Debug("Settings are empty. Opening settings view");
                    // ask user to choose PMS type and location
                    //_settingsViewModel.Show(() =>
                    //{
                    //    loadAppointmentsWorker(firstRun);
                    //});
                }
                else
                {
                    StartPmsIfRequired();
                    InitializeChewsiApi();
                    UpdatePluginRegistration();
                    loadAppointmentsWorker(firstRun);
                }
            }
        }

        public void DeleteAppointment(string id)
        {
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    existing.State = AppointmentState.Deleted;
                    _repository.UpdateAppointment(existing);
                    RefreshAppointments(false, false);
                }
            }
        }
        
        /// <summary>
        /// Loads appointments from PMS (optionally), loads claim statuses from the service (optionally), caches them in local DB
        /// </summary>
        public List<ClaimDto> LoadAppointments(bool loadFromPms, bool loadFromService)
        {
            try
            {
                // load cached in repository items
                var cached = _repository.GetAppointments();

                #region PMS
                if (loadFromPms)
                {
                    // load from PMS
                    var pms = GetDentalApi().GetAppointmentsForToday();
                    // pms.ForEach(m => Logger.Debug($"Loaded appointment from PMS: Date={m.Date}, Id={m.Id}, ChewsiId={m.ChewsiId}"));
                    // cached.ForEach(m => Logger.Debug($"Cached appointment: Date={m.DateTime}, ChewsiId={m.ChewsiId}"));

                    // load subscribers' first names (they are displayed if validation fails)
                    var subscriberNames = pms.Select(m => m.PatientId)
                        .Distinct()
                        .Select(m => GetDentalApi().GetPatientInfo(m))
                        .GroupBy(m => m.ChewsiId)
                        .ToDictionary(m => m.Key, m => m.First().SubscriberFirstName);

                    bool repositoryUpdated = false;

                    // merge PMS items with cached in repository

                    // add new appointments into repository, exist in PMS, not in the DB
                    foreach (var item in pms)
                    {
                        var cachedItem = cached.FirstOrDefault(m => item.Id == m.Id);
                        if (cachedItem == null)
                        {
                            Logger.Debug($"Adding new PMS appointment in the cache Id={item.Id}");
                            _repository.AddAppointment(new Appointment
                            {
                                Id = item.Id,
                                DateTime = item.Date,
                                PmsModifiedDate = item.PmsModifiedDate,
                                State = AppointmentState.TreatmentCompleted,
                                ChewsiId = item.ChewsiId,
                                StatusText = StatusMessage.ReadyToSubmit,
                                PatientId = item.PatientId,
                                PatientName = item.PatientName,
                                ProviderId = item.ProviderId,
                                SubscriberFirstName = subscriberNames[item.ChewsiId]
                            });
                            repositoryUpdated = true;
                        }
                    }

                    // remove old, exist in the DB, not in PMS
                    var old = (from item in cached
                               where pms.All(m => item.ChewsiId != m.ChewsiId)
                               select item.Id).ToList();
                    if (old.Any())
                    {
                        _repository.BulkDeleteAppointments(old);
                        repositoryUpdated = true;
                    }

                    if (repositoryUpdated)
                    {
                        // re-read list from repository
                        cached = _repository.GetAppointments();
                    }
                }
                #endregion

                #region Chewsi claims
                if (loadFromService)
                {
                    // load statuses from Chewsi API service

                    // find providers for submitted appointments
                    var providerIds =
                        cached.Where(m => m.State == AppointmentState.ValidationCompletedAndClaimSubmitted)
                            .GroupBy(m => m.ProviderId)
                            .Select(m => m.Key)
                            .Distinct()
                            .ToList();
                    if (providerIds.Any())
                    {
                        var statuses = GetClaimStatuses(providerIds).SelectMany(m => m).ToList();
                        if (statuses.Any())
                        {
                            _serviceClaims.Clear();
                            foreach (var item in statuses)
                            {
                                var state = AppointmentState.ValidationCompletedAndClaimSubmitted;
                                if (item.Status == "GeneralError" || item.Status == "ValidationError")
                                {
                                    state = AppointmentState.ValidationError;
                                }
                                else if (item.Status == "InvalidProcedureCode")
                                {
                                    state = AppointmentState.ValidationErrorNoResubmit;
                                }

                                _serviceClaims.Add(new ClaimDto()
                                {
                                    //Id = item.Id,
                                    ProviderId = item.ProviderId,
                                    Date = item.PostedOnDate,
                                    PatientName = item.PatientFirstName,
                                    ChewsiId = item.ChewsiID,
                                    State = state,
                                    StatusText = item.MessageToDisplay,
                                    //PatientId = item.PatientId,
                                    SubscriberFirstName = item.SubscriberFirstName,
                                    IsClaimStatus = true,
                                    ClaimNumber = item.Claim_Nbr
                                });

                                lock (_statusWaitListLock)
                                {
                                    if (_statusWaitList.Any())
                                    {
                                        var waitItem = _statusWaitList.FirstOrDefault(
                                            m =>
                                                m.DateTime == item.PostedOnDate.Date
                                                && m.ChewsiId == item.ChewsiID
                                                && m.ProviderId == item.ProviderId);
                                        if (waitItem != null && waitItem.ClaimNumbers.All(m => m != item.Claim_Nbr))
                                        {
                                            // new status for patient's claims has come, remove pending state to stop lookups
                                            _statusWaitList.Remove(waitItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                var result =
                    (from c in cached
                     where c.State != AppointmentState.Deleted && c.State != AppointmentState.ValidationCompletedAndClaimSubmitted
                     orderby c.State == AppointmentState.TreatmentCompleted descending
                     select new ClaimDto
                     {
                         Id = c.Id,
                         ProviderId = c.ProviderId,
                         Date = c.DateTime,
                         PatientName = c.PatientName,
                         ChewsiId = c.ChewsiId,
                         State = c.State,
                         StatusText = c.StatusText,
                         PatientId = c.PatientId,
                         SubscriberFirstName = c.SubscriberFirstName,
                         IsClaimStatus = false,
                         PmsModifiedDate = c.PmsModifiedDate
                         //ClaimNumber = c.ClaimNumber
                     })
                        .Concat(_serviceClaims)
                        .ToList();
                if (Logger.IsDebugEnabled)
                {
                    result.ForEach(m => Logger.Debug($"Loaded appointment VM ChewsiId={m.ChewsiId}, State={m.State}"));
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load appointments");
            }
            return new List<ClaimDto>();
        }
    }
}
