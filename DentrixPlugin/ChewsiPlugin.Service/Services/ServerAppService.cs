using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
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
        private const int AppointmentTtlDays = 1;
        private const int LoadIntervalMs = 10000;

        private readonly object _appointmentsLockObject = new object();
        private readonly object _statusWaitListLock = new object();
        private readonly object _dentalApiInitializationLock = new object();
        private readonly object _initLock = new object();

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
        private ServerState _state;

        public ServerAppService(IRepository repository, IChewsiApi chewsiApi, IDentalApiFactoryService dentalApiFactoryService, 
            IClientCallbackService clientCallbackService)
        {
            _repository = repository;
            _chewsiApi = chewsiApi;
            _dentalApiFactoryService = dentalApiFactoryService;
            _clientCallbackService = clientCallbackService;
            _statusLookupTokenSource = new CancellationTokenSource();
            _loadTokenSource = new CancellationTokenSource();

            Initialize();
        }

        private async void Initialize()
        {
            if (_state != ServerState.Ready)
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
                        await RefreshAppointments(true, true, true).ConfigureAwait(false);
                        if (_state != ServerState.Ready)
                        {
                            lock (_initLock)
                            {
                                if (_state != ServerState.Ready)
                                {
                                    _state = ServerState.Ready;
                                    Task.Factory.StartNew(LoadAppointmentsLoop, _loadTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                                    Task.Factory.StartNew(StatusLookup, _statusLookupTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.Info("Repository is not ready. First run?");
                    _state = ServerState.InvalidSettings;
                }
            }
            InitializeChewsiApi();
            UpdatePluginRegistration();
        }

        public List<ClaimDto> GetClaims(bool force)
        {
            if (force)
            {
                RefreshAppointments(true, true, false)
                    .Wait();
            }
            return _claimItems;
        }

        public ServerState InitClient()
        {
            var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();
            _clientCallbackService.AddClient(OperationContext.Current.Channel.SessionId, callback);
            OperationContext.Current.Channel.Faulted += ChannelOnFaulted;
            return _state;
        }

        public void DisconnectClient()
        {
            var id = OperationContext.Current.Channel.SessionId;
            Logger.Info("Client {0} disconnected", id);
            _clientCallbackService.RemoveClient(id);
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
                result.PmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                return result;
            }
            return null;
        }

        public bool Ping()
        {
            return true;
        }

        public List<DownloadDto> GetDownloads()
        {
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            var address1 = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2 = _repository.GetSettingValue<string>(Settings.Address2Key);
            var list = _chewsiApi.Get835Downloads(new Request835Downloads
            {
                TIN = tin,
                State = state,
                Address = $"{address1} {address2}"
            }).Select(m => new DownloadDto
            {
                Status = m.Status,
                PostedDate = m.PostedDate,
                Edi = m.EDI_835_EDI,
                Report = m.EDI_835_Report
            }).ToList();
            return list;
        }

        public File835Dto DownloadFile(string documentType, string documentId, string postedDate)
        {
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var stream = _chewsiApi.DownloadFile(new DownoadFileRequest
            {
                DocumentType = documentType,
                TIN = tin,
                DocumentID = documentId,
                PostedOnDate = postedDate
            });
            var response = new File835Dto();
            if (stream != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    response.Content = ms.ToArray();
                }
                stream.Close();
            }
            return response;
        }

        public string GetPmsExecutablePath()
        {
            var api = GetDentalApi();
            return api?.GetPmsExecutablePath(_repository.GetSettingValue<string>(Settings.PMS.PathKey));
        }

        public ServerState GetState()
        {
            return _state;
        }

        private void ChannelOnFaulted(object sender, EventArgs eventArgs)
        {
            var ch = sender as IContextChannel;
            _clientCallbackService.RemoveClient(ch.SessionId);
            Logger.Info("Client {0} disconnected after error", ch.SessionId);
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

        private enum ClaimValidationCalled
        {
            Success,
            ServerError,
            ProviderNotFound,
            PatientNotFound,
            ClaimNotFound
        }

        private ClaimValidationCalled ValidateClaim(string id, string patientId,
            out ProviderInformation providerInformation,
            out SubscriberInformation subscriberInformation, out Provider provider,
            out ValidateSubscriberAndProviderResponse result)
        {
            var claim = _repository.GetAppointmentById(id);
            if (claim != null)
            {
                provider = GetDentalApi().GetProvider(claim.ProviderId);
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
                            Logger.Debug(
                                $"Validated subscriber '{patientId}' and provider '{claim.ProviderId}': '{result.ValidationPassed}'");
                            return ClaimValidationCalled.Success;
                        }
                        Logger.Debug(
                            $"Failed to validate subscriber '{patientId}' and provider '{claim.ProviderId}': invalid server response");
                        return ClaimValidationCalled.ServerError;
                    }
                    providerInformation = null;
                    subscriberInformation = null;
                    result = null;
                    return ClaimValidationCalled.PatientNotFound;
                }
                providerInformation = null;
                subscriberInformation = null;
                result = null;
                return ClaimValidationCalled.ProviderNotFound;
            }
            providerInformation = null;
            subscriberInformation = null;
            result = null;
            provider = null;
            return ClaimValidationCalled.ClaimNotFound;
        }

        private SubmitClaimResult SubmitClaim(string id, DateTime appointmentDate, string patientId, ProviderInformation providerInformation, SubscriberInformation subscriberInformation, DateTime pmsModifiedDate)
        {
            var procedures = GetDentalApi().GetProcedures(patientId, id, appointmentDate);
            if (procedures.Any())
            {
                var claimStatuses = _chewsiApi.RetrievePluginClientRowStatuses(GetSettings().Tin);
                var status = claimStatuses.FirstOrDefault(m => m.PMSClaimNbr == id 
                && Utils.ArePMSModifiedDatesEqual(DateTime.Parse(m.PMSModifiedDate), pmsModifiedDate));
                if (status != null)
                {
                    var s = (PluginClientRowStatus.Statuses)Enum.Parse(typeof (PluginClientRowStatus.Statuses), status.Status);
                    switch (s)
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
                    await RefreshAppointments(false, true, true).ConfigureAwait(false);
                }
                Utils.SleepWithCancellation(_statusLookupTokenSource.Token, StatusRefreshIntervalMs);
            }
        }

        public void Dispose()
        {
            _statusLookupTokenSource.Cancel();
            _loadTokenSource.Cancel();
            _dentalApi?.Unload();
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
                                Logger.Debug("Loaded new claims ({0}), notify clients = {1}", list.Count, notifyClients);
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
            }).ConfigureAwait(false);
        }

        private async void LoadAppointmentsLoop()
        {
            // stop if already canceled
            _loadTokenSource.Token.ThrowIfCancellationRequested();

            while (true)
            {
                // Refresh appointments every 3 minutes
                Utils.SleepWithCancellation(_loadTokenSource.Token, LoadIntervalMs);

                await RefreshAppointments(true, true, true).ConfigureAwait(false);
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
                await RefreshAppointments(false, false, true).ConfigureAwait(false);
            }
        }

        private static class StatusMessage
        {
            public const string PaymentProcessing = "Payment processing...";
            public const string PaymentProcessingError = "Payment processing failed: invalid server response";
            public const string ReadyToSubmit = "Please submit this claim..";
        }

        public SubmitClaimResult ValidateAndSubmitClaim(string id)
        {
            SubmitClaimResult result = SubmitClaimResult.Error;
            
            lock (_appointmentsLockObject)
            {
                var claim = _repository.GetAppointmentById(id);
                if (claim == null)
                {
                    throw new FaultException("Cannot find claim");
                }

                SetAppointmentState(id, AppointmentState.TreatmentCompleted, StatusMessage.PaymentProcessing);
                ValidateSubscriberAndProviderResponse validationResponse;
                ProviderInformation providerInformation;
                SubscriberInformation subscriberInformation;
                Provider provider;
                var serverCalled = ValidateClaim(id, claim.PatientId, out providerInformation,
                    out subscriberInformation, out provider, out validationResponse);
                switch (serverCalled)
                {
                    case ClaimValidationCalled.Success:
                        // got response from Chewsi API, analyze it
                        if (validationResponse.ValidationPassed)
                        {
                            providerInformation.Id = validationResponse.ProviderID;
                            providerInformation.OfficeNbr = validationResponse.OfficeNumber;
                            result = SubmitClaim(id, claim.DateTime, claim.PatientId, providerInformation, subscriberInformation,
                                claim.PmsModifiedDate);
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

                            SetAppointmentState(id, AppointmentState.ValidationError, statusText);
                        }
                        break;
                    case ClaimValidationCalled.ServerError:
                        SetAppointmentState(id, AppointmentState.ValidationError, StatusMessage.PaymentProcessingError);
                        break;
                    case ClaimValidationCalled.ProviderNotFound:
                        SetAppointmentState(id, AppointmentState.ValidationError, "Cannot find patient in PMS");
                        break;
                    case ClaimValidationCalled.PatientNotFound:
                        SetAppointmentState(id, AppointmentState.ValidationError, "Cannot find provider in PMS");
                        break;
                    case ClaimValidationCalled.ClaimNotFound:
                        SetAppointmentState(id, AppointmentState.ValidationError, "Cannot find claim");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                RefreshAppointments(true, true, true);
                return result;
            }
        }

        public SettingsDto GetSettings()
        {
            var pmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
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
            return new SettingsDto(pmsType, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state, startPms, machineId);
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
                _repository.SaveSetting(Settings.Address1Key, settingsDto.Address1);
                _repository.SaveSetting(Settings.Address2Key, settingsDto.Address2);
                _repository.SaveSetting(Settings.StateKey, settingsDto.State);
                _repository.SaveSetting(Settings.OsKey, Utils.GetOperatingSystemInfo());
                _repository.SaveSetting(Settings.AppVersionKey, Utils.GetPluginVersion());
                
                _repository.SaveSetting(Settings.StartPms, settingsDto.StartPms);
                _repository.SaveSetting(Settings.UseProxy, settingsDto.UseProxy);
                _repository.SaveSetting(Settings.ProxyAddress, settingsDto.ProxyAddress);
                _repository.SaveSetting(Settings.ProxyPort, settingsDto.ProxyPort);
                _repository.SaveSetting(Settings.ProxyLogin, settingsDto.ProxyLogin);
                _repository.SaveSetting(Settings.ProxyPassword, settingsDto.ProxyPassword);

                // init DentalApi and get version
                _repository.SaveSetting(Settings.PMS.VersionKey, GetDentalApi().GetVersion());

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
            bool deleted = false;
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    var provider = GetProvider(existing.ProviderId);
                    if (_chewsiApi.StorePluginClientRowStatus(new PluginClientRowStatus
                    {
                        TIN = provider.Tin,
                        Status = PluginClientRowStatus.Statuses.D.ToString(),
                        PMSModifiedDate = existing.PmsModifiedDate.ToString("G"),
                        PMSClaimNbr = existing.Id
                    }))
                    {
                        existing.State = AppointmentState.Deleted;
                        _repository.UpdateAppointment(existing);
                        deleted = true;
                    }
                }
            }

            if (deleted)
            {
                RefreshAppointments(false, false, true);
            }
            return deleted;
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
                    var subscriberNames =
                        pms.Select(m => m.PatientId)
                            .Distinct()
                            .Select(m => GetDentalApi().GetPatientInfo(m))
                            .GroupBy(m => m.ChewsiId)
                            .ToDictionary(m => m.Key, m => m.First().SubscriberFirstName);

                    bool repositoryUpdated = false;

                    // merge PMS items with cached in repository

                    // add new appointments into repository, exist in PMS, not in the DB
                    foreach (var item in pms)
                    {
                        if (!_repository.AppointmentExists(item.Id))
                        {
                            Logger.Debug($"Adding new PMS appointment into the cache Id={item.Id}");
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

                                _serviceClaims.Add(new ClaimDto
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
                                        var waitItem =
                                            _statusWaitList.FirstOrDefault(
                                                m =>
                                                    m.DateTime == item.PostedOnDate.Date && m.ChewsiId == item.ChewsiID &&
                                                    m.ProviderId == item.ProviderId);
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
                    where
                        c.State != AppointmentState.Deleted &&
                        c.State != AppointmentState.ValidationCompletedAndClaimSubmitted
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
                        PmsModifiedDate = c.PmsModifiedDate,
                        //ClaimNumber = c.ClaimNumber
                    }).Concat(_serviceClaims)
                    // don't return deleted and submitted claims
                    .Where(
                        m => !clientRowStatuses.Any(n => Utils.ArePMSModifiedDatesEqual(DateTime.Parse(n.PMSModifiedDate), m.PmsModifiedDate)
                                     && n.PMSClaimNbr == m.Id))
                    .ToList();
                if (Logger.IsDebugEnabled)
                {
                    result.ForEach(m => Logger.Debug($"Loaded appointment ChewsiId={m.ChewsiId}, Date={m.Date}, PMSDate={m.PmsModifiedDate}, PMS#={m.Id}, State={m.State}"));
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