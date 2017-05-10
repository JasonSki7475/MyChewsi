using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using Microsoft.Win32;
using NLog;
using Appointment = ChewsiPlugin.Api.Repository.Appointment;

namespace ChewsiPlugin.Service.Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
    internal class ServerAppService : IServerAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const int StatusLookupTimeoutSeconds = 60;
        private const int StatusRefreshIntervalMs = 10000;
        private const string ChewsiLauncherRegistryKey = "Chewsi Launcher";
        private const string ChewsiLauncherExecutableName = "ChewsiPlugin.Launcher.exe";
        private const int AppointmentTtlDays = 1;
        private const int LoadIntervalMs = 180000;

        private readonly object _appointmentsLockObject = new object();
        private readonly object _statusWaitListLock = new object();
        private readonly object _dentalApiInitializationLock = new object();

        private readonly List<ClaimDto> _serviceClaims = new List<ClaimDto>();
        private readonly List<StatusLookupWaitItem> _statusWaitList = new List<StatusLookupWaitItem>();
        private readonly List<ClaimDto> _claimItems = new List<ClaimDto>();
        private readonly IRepository _repository;
        private readonly IChewsiApi _chewsiApi;
        private bool _loadingAppointments;
        private readonly CancellationTokenSource _statusLookupTokenSource;
        private readonly CancellationTokenSource _loadTokenSource;
        private IDentalApi _dentalApi;
        private Settings.PMS.Types _pmsType;
        private readonly IDentalApiFactoryService _dentalApiFactoryService;
        private readonly IClientCallbackService _clientCallbackService;
        private readonly IDialogService _dialogService;
        private ServerState _state;

        public ServerAppService(IRepository repository, IChewsiApi chewsiApi, IDentalApiFactoryService dentalApiFactoryService, 
            IClientCallbackService clientCallbackService, IDialogService dialogService)
        {
            _repository = repository;
            _chewsiApi = chewsiApi;
            _dentalApiFactoryService = dentalApiFactoryService;
            _clientCallbackService = clientCallbackService;
            _dialogService = dialogService;
            _statusLookupTokenSource = new CancellationTokenSource();
            _loadTokenSource = new CancellationTokenSource();

            Initialize();
        }

        private async void Initialize()
        {
            _state = ServerState.Initializing;
            Logger.Info("Initializing service");
            _repository.Initialize();
            if (_repository.Ready)
            {
                Logger.Info("Repository is ready. Loading dental API");
                _dentalApi = GetDentalApi();
                if (_dentalApi != null)
                {
                    DeleteOldAppointments();
                    StartPmsIfRequired();
                    InitializeChewsiApi();
                    UpdatePluginRegistration();
                    await RefreshAppointments(true, true, false);
                    _state = ServerState.Ready;
                    await Task.Factory.StartNew(LoadAppointmentsLoop, _loadTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                    await Task.Factory.StartNew(StatusLookup, _statusLookupTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                }
            }
            else
            {
                Logger.Info("Repository is not ready. First run?");
                _state = ServerState.InvalidSettings;
            }
        }

        public List<ClaimDto> GetClaims(bool force)
        {
            if (force)
            {
                // old items are returned immediately, new items will be broadcasted soon
                RefreshAppointments(true, true, true);
            }
            return _claimItems;
        }

        public ServerState InitClient()
        {
            if (_dentalApi != null)
            {
                var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();
                _clientCallbackService.AddClient(OperationContext.Current.Channel.SessionId, callback);                
            }
            return _state;
        }

        public void DisconnectClient()
        {
            OperationContext.Current.Channel.Faulted += ChannelOnFaulted;
            _clientCallbackService.RemoveClient(OperationContext.Current.Channel.SessionId);
        }

        public Provider GetProvider(string providerId)
        {
            return _dentalApi.GetProvider(providerId);
        }

        public InitialSettingsDto GetInitialSettings()
        {
            if (_state == ServerState.InvalidSettings)
            {
                var result = new InitialSettingsDto();
                var api = GetDentalApi();
                if (api != null)
                {
                    var list = api.GetAppointmentsForToday();
                    if (list.Count > 0)
                    {
                        var p = GetProvider(list[0].ProviderId);
                        if (p != null)
                        {
                            result.State = p.State;
                            result.Tin = p.Tin;
                            result.AddressLine1 = p.AddressLine1;
                            result.AddressLine2 = p.AddressLine2;                            
                        }
                    }
                }
                result.IsClient = _repository.GetSettingValue<bool>(Settings.IsClient);
                result.PmsPath = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
                result.PmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                return result;
            }
            return null;
        }

        public ServerState GetState()
        {
            return _state;
        }

        private void ChannelOnFaulted(object sender, EventArgs eventArgs)
        {
            var ch = sender as IContextChannel;
            _clientCallbackService.RemoveClient(ch.SessionId);
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

        public enum ClaimValidationResult
        {
            Success,
            Fail,
            ServerError,
            ProviderNotFound,
            PatientNotFound
        }
        
        private ClaimValidationResult ValidateClaim(string providerId, string patientId, out ProviderInformation providerInformation, 
            out SubscriberInformation subscriberInformation, out Provider provider, out ValidateSubscriberAndProviderResponse result)
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
                    result = _chewsiApi.ValidateSubscriberAndProvider(providerInformation, providerAddress, subscriberInformation);
                    if (result != null)
                    {
                        Logger.Debug($"Validated subscriber '{patientId}' and provider '{providerId}': '{result.ValidationPassed}'");
                        return ClaimValidationResult.Success;
                    }
                    Logger.Debug($"Failed to validate subscriber '{patientId}' and provider '{providerId}': invalid server response");
                    return ClaimValidationResult.ServerError;
                }
                providerInformation = null;
                subscriberInformation = null;
                result = null;
                return ClaimValidationResult.PatientNotFound;
            }
            providerInformation = null;
            subscriberInformation = null;
            result = null;
            return ClaimValidationResult.ProviderNotFound;
        }

        private SubmitClaimResult SubmitClaim(string id, DateTime appointmentDate, string patientId, ProviderInformation providerInformation, SubscriberInformation subscriberInformation, DateTime pmsModifiedDate)
        {
            var procedures = GetDentalApi().GetProcedures(patientId, id, appointmentDate);
            if (procedures.Any())
            {
                var claimStatuses = _chewsiApi.RetrievePluginClientRowStatuses(GetSettings().Tin);
                var status = claimStatuses.FirstOrDefault(m => m.PMSClaimNbr == id && m.PMSModifiedDate == pmsModifiedDate);
                if (status != null)
                {
                    switch (status.Status)
                    {
                        case PluginClientRowStatus.Statuses.S:
                            return SubmitClaimResult.AlreadySubmitted;
                        case PluginClientRowStatus.Statuses.D:
                            return SubmitClaimResult.AlreadyDeleted;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    // Since we cannot match claim number to appointment (ProcessClaim response is empty; claim number was expected to be there, but API developers cannot implement it so)
                    // We have to save current claim numbers for providerId+chewsiId+date+claimnumbers and wait till one more status appear for this combination in status lookup response; or till timeout
                    lock (_statusWaitListLock)
                    {
                        _statusWaitList.Add(new StatusLookupWaitItem(subscriberInformation.Id, providerInformation.Id,
                            appointmentDate.Date,
                            _claimItems.Where(
                                m =>
                                    m.IsClaimStatus && m.ProviderId == providerInformation.Id &&
                                    m.ChewsiId == subscriberInformation.Id && m.Date.Date == appointmentDate.Date)
                                .Select(m => m.ClaimNumber)
                                .ToList()));
                    }
                    _chewsiApi.ProcessClaim(id, providerInformation, subscriberInformation,
                        procedures.Select(m => new ClaimLine(m.Date, m.Code, m.Amount)).ToList(), pmsModifiedDate);
                    SetAppointmentState(id, AppointmentState.ValidationCompletedAndClaimSubmitted);
                    RefreshAppointments(false, false, true);
                    Logger.Debug($"Processed claim, found '{procedures.Count}' procedures.");
                }
                return SubmitClaimResult.Ok;
            }
            else
            {
                Logger.Error("Cannot find any procedures for the patient" + " " + patientId);
                return SubmitClaimResult.AlreadyDeleted;
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
                    TIN = provider.Tin, State = provider.State, Address = provider.AddressLine1
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

        private async void StatusLookup()
        {
            // stop if already canceled
            _statusLookupTokenSource.Token.ThrowIfCancellationRequested();

            while (true)
            {
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
                    await RefreshAppointments(false, true, true);
                }
                Utils.SleepWithCancellation(_statusLookupTokenSource.Token, StatusRefreshIntervalMs);
            }
        }

        public void Dispose()
        {
            _statusLookupTokenSource.Cancel();
            _loadTokenSource.Cancel();
        }

        private async Task<bool> RefreshAppointments(bool loadFromPms, bool loadFromService, bool notifyClients)
        {
            return await Task<bool>.Factory.StartNew(() =>
            {
                if (!_loadingAppointments)
                {
                    lock (_appointmentsLockObject)
                    {
                        if (!_loadingAppointments)
                        {
                            _loadingAppointments = true;
                            try
                            {
                                var list = LoadAppointments(loadFromPms, loadFromService);
                                _claimItems.Clear();
                                _claimItems.AddRange(list);
                                if (notifyClients)
                                {
                                    _clientCallbackService.SetClaims(list);
                                }
                            }
                            finally
                            {
                                _loadingAppointments = false;
                            }
                        }
                    }
                }
                return true;
            });
        }

        private async void LoadAppointmentsLoop()
        {
            // stop if already canceled
            _loadTokenSource.Token.ThrowIfCancellationRequested();

            while (true)
            {
                // Refresh appointments every 3 minutes
                Utils.SleepWithCancellation(_loadTokenSource.Token, LoadIntervalMs);

                await RefreshAppointments(true, true, true);
            }
        }

        private async void SetAppointmentState(string id, AppointmentState? state = null, string statusText = null)
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
                await RefreshAppointments(false, false, true);
            }
        }

        private static class StatusMessage
        {
            public const string PaymentProcessing = "Payment processing...";
            public const string PaymentProcessingError = "Payment processing failed";
            public const string ReadyToSubmit = "Please submit this claim..";
        }

        public SubmitClaimResult ValidateAndSubmitClaim(string id)
        {
            ProviderInformation providerInformation;
            SubscriberInformation subscriberInformation;
            Provider provider;
            SubmitClaimResult result = SubmitClaimResult.Error;

            var claim = _claimItems.FirstOrDefault(m => m.Id == id);
            if (claim == null)
            {
                throw new FaultException("Cannot find claim");
            }

            SetAppointmentState(id, AppointmentState.TreatmentCompleted, StatusMessage.PaymentProcessing);
            ValidateSubscriberAndProviderResponse validationResponse;
            var validationResult = ValidateClaim(id, claim.PatientId, out providerInformation, out subscriberInformation, out provider, out validationResponse);
            switch (validationResult)
            {
                case ClaimValidationResult.Success:
                    providerInformation.Id = validationResponse.ProviderID;
                    providerInformation.OfficeNbr = validationResponse.OfficeNumber;
                    result = SubmitClaim(id, claim.Date, claim.PatientId, providerInformation, subscriberInformation, claim.PmsModifiedDate);
                    break;
                case ClaimValidationResult.Fail:
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
                    SetAppointmentState(id, AppointmentState.ValidationError, statusText);
                    break;
                case ClaimValidationResult.ServerError:
                    SetAppointmentState(id, AppointmentState.ValidationError, StatusMessage.PaymentProcessingError);
                    break;
                case ClaimValidationResult.ProviderNotFound:
                    SetAppointmentState(id, AppointmentState.ValidationError, "Cannot find patient in PMS");
                    break;
                case ClaimValidationResult.PatientNotFound:
                    SetAppointmentState(id, AppointmentState.ValidationError, "Cannot find provider in PMS");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            RefreshAppointments(true, true, true);
            return result;
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
            return new SettingsDto(pmsType, pmsPath, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state, startPms, GetLauncherStartup(), machineId, isClient);
        }

        private void StartPmsIfRequired()
        {
            var type = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var start = _repository.GetSettingValue<bool>(Settings.StartPms);
            if (start || type == Settings.PMS.Types.Eaglesoft)
            {
                Logger.Info("Starting PMS");
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
                if (pmsVersion != pmsVersionOld || osOld != Utils.GetOperatingSystemInfo() || pluginVersionOld != Utils.GetPluginVersion())
                {
                    Logger.Info("Configuration changes detected. Updating plugin registration...");
                    var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                    _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, address1Old, address2Old, pmsTypeOld, pmsVersion));
                    Logger.Info("Plugin registration successfully updated");
                }
            }
        }

        public bool SaveSettings(SettingsDto settingsDto)
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

                var firstAppRun = !_repository.Ready;
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
                        if ((pmsVersion != pmsVersionOld) || pmsTypeOld != settingsDto.PmsType || address1Old != settingsDto.Address1 || address2Old != settingsDto.Address2 || stateOld != settingsDto.State || osOld != Utils.GetOperatingSystemInfo() || tinOld != settingsDto.Tin || pluginVersionOld != Utils.GetPluginVersion())
                        {
                            Logger.Info("Configuration changes detected. Updating plugin registration...");
                            var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                            _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                        }
                    }
                    else
                    {
                        Logger.Info("Registering plugin...");
                        var machineId = _chewsiApi.RegisterPlugin(new RegisterPluginRequest(settingsDto.Tin, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                        Logger.Info("Machine Id: " + machineId);
                        _repository.SaveSetting(Settings.MachineIdKey, machineId);
                    }
                }

                Initialize();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving settings");
                return false;
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
            Logger.Info("Initializing Chewsi API");
            var s = GetSettings();
            _chewsiApi.Initialize(s.MachineId, s.UseProxy, s.ProxyAddress, s.ProxyPort, s.ProxyLogin, s.ProxyPassword);
        }

        public bool DeleteAppointment(string id)
        {
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    var p = GetProvider(existing.ProviderId);
                    if (_chewsiApi.StorePluginClientRowStatus(new PluginClientRowStatus
                    {
                        TIN = p.Tin, Status = PluginClientRowStatus.Statuses.D, PMSModifiedDate = existing.PmsModifiedDate, PMSClaimNbr = existing.Id
                    }))
                    {
                        existing.State = AppointmentState.Deleted;
                        _repository.UpdateAppointment(existing);
                        RefreshAppointments(false, false, true);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Loads appointments from PMS (optionally), loads claim statuses from the service (optionally), caches them in local DB
        /// </summary>
        private List<ClaimDto> LoadAppointments(bool loadFromPms, bool loadFromService)
        {
            Logger.Info("Loading appointments");
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
                    var subscriberNames = pms.Select(m => m.PatientId).Distinct().Select(m => GetDentalApi().GetPatientInfo(m)).GroupBy(m => m.ChewsiId).ToDictionary(m => m.Key, m => m.First().SubscriberFirstName);

                    bool repositoryUpdated = false;

                    // merge PMS items with cached in repository

                    // add new appointments into repository, exist in PMS, not in the DB
                    foreach (var item in pms)
                    {
                        if (!_repository.AppointmentExists(item.Id))
                        {
                            Logger.Debug($"Adding new PMS appointment in the cache Id={item.Id}");
                            _repository.AddAppointment(new Appointment
                            {
                                Id = item.Id, DateTime = item.Date, PmsModifiedDate = item.PmsModifiedDate, State = AppointmentState.TreatmentCompleted, ChewsiId = item.ChewsiId, StatusText = StatusMessage.ReadyToSubmit, PatientId = item.PatientId, PatientName = item.PatientName, ProviderId = item.ProviderId, SubscriberFirstName = subscriberNames[item.ChewsiId]
                            });
                            repositoryUpdated = true;
                        }
                    }

                    // remove old, exist in the DB, not in PMS
                    var old = (from item in cached where pms.All(m => item.ChewsiId != m.ChewsiId) select item.Id).ToList();
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

                List<PluginClientRowStatus> clientRowStatuses = new List<PluginClientRowStatus>();

                if (loadFromService)
                {
                    // load statuses from Chewsi API service
                    clientRowStatuses.AddRange(_chewsiApi.RetrievePluginClientRowStatuses(GetSettings().Tin));

                    // find providers for submitted appointments
                    var providerIds = cached.Where(m => m.State == AppointmentState.ValidationCompletedAndClaimSubmitted).GroupBy(m => m.ProviderId).Select(m => m.Key).Distinct().ToList();
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

                                _serviceClaims.Add(new ClaimDto
                                {
                                    //Id = item.Id,
                                    ProviderId = item.ProviderId, Date = item.PostedOnDate, PatientName = item.PatientFirstName, ChewsiId = item.ChewsiID, State = state, StatusText = item.MessageToDisplay,
                                    //PatientId = item.PatientId,
                                    SubscriberFirstName = item.SubscriberFirstName, IsClaimStatus = true, ClaimNumber = item.Claim_Nbr
                                });

                                lock (_statusWaitListLock)
                                {
                                    if (_statusWaitList.Any())
                                    {
                                        var waitItem = _statusWaitList.FirstOrDefault(m => m.DateTime == item.PostedOnDate.Date && m.ChewsiId == item.ChewsiID && m.ProviderId == item.ProviderId);
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

                var result = (from c in cached
                    where c.State != AppointmentState.Deleted && c.State != AppointmentState.ValidationCompletedAndClaimSubmitted
                    orderby c.State == AppointmentState.TreatmentCompleted descending
                    select new ClaimDto
                    {
                        Id = c.Id, ProviderId = c.ProviderId, Date = c.DateTime, PatientName = c.PatientName, ChewsiId = c.ChewsiId, State = c.State, StatusText = c.StatusText, PatientId = c.PatientId, SubscriberFirstName = c.SubscriberFirstName, IsClaimStatus = false, PmsModifiedDate = c.PmsModifiedDate,
                        //ClaimNumber = c.ClaimNumber
                    }).Concat(_serviceClaims)
                    // don't return deleted and submitted claims
                    .Where(m => !clientRowStatuses.Any(n => n.PMSModifiedDate == m.PmsModifiedDate && n.PMSClaimNbr == m.Id)).ToList();
                if (Logger.IsDebugEnabled)
                {
                    result.ForEach(m => Logger.Debug($"Loaded appointment ChewsiId={m.ChewsiId}, State={m.State}"));
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