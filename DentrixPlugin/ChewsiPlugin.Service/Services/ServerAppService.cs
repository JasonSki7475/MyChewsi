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
        private const int AppointmentTtlDays = 1;
        private const int LoadIntervalMs = 10000;

        private readonly object _appointmentsLockObject = new object();
        private readonly object _dentalApiInitializationLock = new object();
        private readonly object _initLock = new object();
        
        private readonly IRepository _repository;
        private readonly IChewsiApi _chewsiApi;
        private bool _loadingAppointments;
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
                        DeleteOldRecords();
                        await RefreshAppointments().ConfigureAwait(false);
                        if (_state != ServerState.Ready)
                        {
                            lock (_initLock)
                            {
                                if (_state != ServerState.Ready)
                                {
                                    _state = ServerState.Ready;
                                    Task.Factory.StartNew(LoadAppointmentsLoop, _loadTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
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

        public void ReloadClaims()
        {
            RefreshAppointments().ConfigureAwait(false);
        }

        public ServerState InitClient()
        {
            var callback = OperationContext.Current.GetCallbackChannel<IClientCallback>();
            _clientCallbackService.AddClient(GetChannelId(), callback);
            Logger.Info("Client '{0}' connected, state={1}", GetChannelId(), _state);
            OperationContext.Current.Channel.Faulted += ChannelOnFaulted;
            return _state;
        }

        public void DisconnectClient()
        {
            var id = GetChannelId();
            Logger.Info("Client '{0}' disconnected", id);
            _clientCallbackService.RemoveClient(id);
        }

        public InitialSettingsDto GetInitialSettings()
        {
            Logger.Info("Client '{0}' requested initial settings", GetChannelId());
            if (_state == ServerState.InvalidSettings)
            {
                var result = new InitialSettingsDto();
                var api = GetDentalApi();
                if (api != null)
                {
                    // find an appointment in last 3 days
                    var list = api.GetAppointments(DateTime.Today);
                    if (list.Count == 0)
                    {
                        list = api.GetAppointments(DateTime.Today.AddDays(-1));
                    }
                    if (list.Count == 0)
                    {
                        list = api.GetAppointments(DateTime.Today.AddDays(-2));
                    }

                    if (list.Count > 0)
                    {
                        var p = _dentalApi.GetProvider(list[0].ProviderId);
                        if (p != null)
                        {
                            result.City = p.City;
                            result.Zip = p.ZipCode;
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
            Logger.Info("Client '{0}' loaded downloads list", GetChannelId());
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            var address1 = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2 = _repository.GetSettingValue<string>(Settings.Address2Key);
            var response = _chewsiApi.Get835Downloads(new Request835Downloads
            {
                TIN = tin,
                State = state,
                Address = $"{address1} {address2}"
            });
            if (response != null)
            {
                return response.Select(m => new DownloadDto
                {
                    Status = m.Status,
                    PostedDate = m.PostedDate,
                    Edi = m.EDI_835_EDI,
                    Report = m.EDI_835_Report
                }).ToList();
            }
            return new List<DownloadDto>();
        }

        public List<PaymentPlanHistoryDto> GetPayments()
        {
            Logger.Info("Client '{0}' loaded payments list", GetChannelId());
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var paymentPlanHistory = _chewsiApi.GetOrthoPaymentPlanHistory(tin);
            if (paymentPlanHistory != null)
            {
                return paymentPlanHistory.Select(m => new PaymentPlanHistoryDto
                {
                    PaymentSchedule = m.PaymentSchedule,
                    ChewsiId = m.ChewsiID,
                    Provider = m.ProviderFirstName,
                    PatientFirstName = m.PatientFirstName,
                    LastPaymentOn = m.LastPaymentOn,
                    PostedOn = DateTime.Parse(m.PostedOn),
                    BalanceRemaining = m.BalanceRemaining,
                    NextPaymentOn = m.NextPaymentOn,
                    Items = m.Items?.Select(i => new PaymentPlanHistoryItemDto
                    {
                        ChewsiFeeAmount = i.ChewsiFeeAmount,
                        PaymentSchedule = i.PaymentSchedule,
                        PatientPaymentOf = i.PatientPaymentOf,
                        PaymentMadeOn = i.PaymentMadeOn,
                        ProviderReceives = i.ProviderReceives
                    }).ToList() ?? new List<PaymentPlanHistoryItemDto>()
                }).ToList();                
            }
            return new List<PaymentPlanHistoryDto>();
        }

        public File835Dto DownloadFile(string documentType, string documentId, string postedDate)
        {
            Logger.Info("Client '{0}' downloaded file {1}", GetChannelId(), documentId);
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

        private static readonly string[] ProceduresForPayments = { "8010", "8020", "8030", "8040", "8050", "8060", "8070", "8080", "8090" };

        public CalculatedPaymentsDto GetCalculatedPayments(string id, double downPayment, int numberOfPayments, DateTime firstMonthlyPaymentDate)
        {
            Logger.Info("Client '{0}' requested payments calculation for {1}", GetChannelId(), id);
            var claim = _repository.GetAppointmentById(id);
            if (claim != null)
            {
                var procedures = GetDentalApi().GetProcedures(claim.PatientId, id, claim.DateTime);
                var procedure = procedures.FirstOrDefault(m => ProceduresForPayments.Any(p => p == m.Code));
                Logger.Info("GetCalculatedPayments: Found total {0} procedures, {1} payable procedure ({2}) for {3}", procedures.Count, procedure != null ? 1:0, procedure?.Code, id);
                if (procedure != null)
                {
                    var response = _chewsiApi.GetCalculatedOrthoPayments(new CalculatedOrthoPaymentsRequest
                    {
                        NumberOfPayments = numberOfPayments,
                        FirstMonthlyPaymentDate = firstMonthlyPaymentDate.ToString("d"),
                        DownPaymentAmount = downPayment,
                        DateOfService = procedure.Date.ToString("d"),
                        ProcedureCode = procedure.Code,
                        ProcedureCharge = procedure.Amount.ToString("F")
                    });
                    if (response != null)
                    {
                        return new CalculatedPaymentsDto
                        {
                            ChewsiMonthlyFee = response.ChewsiMonthlyFee,
                            SubscribersReoccuringMonthlyCharge = response.SubscribersReoccuringMonthlyCharge,
                            TotalProviderReimbursement = response.TotalProviderReimbursement,
                            TotalProviderSubmittedCharge = response.TotalProviderSubmittedCharge,
                            TotalSubscriberCharge = response.TotalSubscriberCharge
                        };
                    }
                }
            }
            return null;
        }

        public string GetPmsExecutablePath()
        {
            var api = GetDentalApi();
            var path = api?.GetPmsExecutablePath(_repository.GetSettingValue<string>(Settings.PMS.PathKey));
            Logger.Info("Client '{0}' requested PMS path '{1}'", GetChannelId(), path);
            return path;
        }

        public ServerState GetState()
        {
            return _state;
        }

        private void ChannelOnFaulted(object sender, EventArgs eventArgs)
        {
            var ch = sender as IContextChannel;
            _clientCallbackService.RemoveClient(ch.SessionId);
            Logger.Info("Client '{0}' disconnected after channel fault", ch.SessionId);
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
                        Logger.Debug("Initializing '{0}' API", pmsTypeString);
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
            out SubscriberInformation subscriberInformation, 
            out ValidateSubscriberAndProviderResponse result)
        {
            var claim = _repository.GetAppointmentById(id);
            if (claim != null)
            {
                var tin = _repository.GetSettingValue<string>(Settings.TIN);
                var state = _repository.GetSettingValue<string>(Settings.StateKey);
                var address1 = _repository.GetSettingValue<string>(Settings.Address1Key);
                var address2 = _repository.GetSettingValue<string>(Settings.Address2Key);
                var city = _repository.GetSettingValue<string>(Settings.City);
                var zip = _repository.GetSettingValue<string>(Settings.Zip);

                var provider = GetDentalApi().GetProvider(claim.ProviderId);
                if (provider != null)
                {
                    var patientInfo = GetDentalApi().GetPatientInfo(patientId);
                    if (patientInfo != null)
                    {
                        providerInformation = new ProviderInformation
                        {
                            NPI = provider.Npi,
                            TIN = tin
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
                            RenderingAddress1 = address1,
                            RenderingAddress2 = address2,
                            RenderingCity = city,
                            RenderingState = state,
                            RenderingZip = zip
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
            return ClaimValidationCalled.ClaimNotFound;
        }

        private SubmitClaimResult SubmitClaim(string id, DateTime appointmentDateTime, string patientId, ProviderInformation providerInformation, SubscriberInformation subscriberInformation, DateTime pmsModifiedDate, double downPayment, int numberOfPayments)
        {
            var procedures = GetDentalApi().GetProcedures(patientId, id, appointmentDateTime);
            bool elegibleForPayments = procedures.Any(m => ProceduresForPayments.Any(p => p == m.Code));
            var submittedProcedures = _repository.GetSubmittedProcedures(patientId, providerInformation.Id, appointmentDateTime.Date).ToList();
            int total = procedures.Count;
            procedures = procedures
                .Where(p => !submittedProcedures.Any(s => s.Code == p.Code && Utils.AreAmountsEqual(s.Amount, p.Amount)))
                .ToList();
            Logger.Info("SubmitClaim: found {0} procedures ({1} submitted) for the appointment {2}", total, submittedProcedures.Count, id);
            if (procedures.Count > 0)
            {
                var tin = _repository.GetSettingValue<string>(Settings.TIN);
                var claimStatuses = _chewsiApi.RetrievePluginClientRowStatuses(tin);
                var status = claimStatuses.FirstOrDefault(m => m.PMSClaimNbr == id && Utils.ArePmsModifiedDatesEqual(DateTime.Parse(m.PMSModifiedDate), pmsModifiedDate));
                if (status != null)
                {
                    var s = (PluginClientRowStatus.Statuses)Enum.Parse(typeof (PluginClientRowStatus.Statuses), status.Status);
                    switch (s)
                    {
                        case PluginClientRowStatus.Statuses.S:
                            Logger.Info("SubmitClaim: claim {0} has already been submitted", id);
                            return SubmitClaimResult.AlreadySubmitted;
                        case PluginClientRowStatus.Statuses.D:
                            Logger.Info("SubmitClaim: claim {0} has already been deleted", id);
                            return SubmitClaimResult.AlreadyDeleted;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    // Since we cannot match claim number to appointment (ProcessClaim response is empty; claim number was expected to be there, but API developers cannot implement it so)
                    // [Removed] We have to save current claim numbers for providerId+chewsiId+date+claimnumbers and wait till one more status appear for this combination in status lookup response; or till timeout
                    _chewsiApi.ProcessClaim(id, providerInformation, subscriberInformation, 
                        procedures.Select(m => new ClaimLine(m.Date, m.Code, m.Amount)).ToList(), pmsModifiedDate, downPayment, numberOfPayments, elegibleForPayments);
                    SetAppointmentState(id, AppointmentState.ValidationCompletedAndClaimSubmitted);
                    Logger.Info("SubmitClaim: claim {0} has been sent to the API. Submitted procedures: {1}", id, string.Join(",", procedures.Select(m => m.Code)));
                    _repository.AddSubmittedProcedures(procedures.Select(m => new SubmittedProcedure
                    {
                        Date = m.Date,
                        PatientId = patientId,
                        ProviderId = providerInformation.Id,
                        Amount = m.Amount,
                        Code = m.Code
                    }));
                    Thread.Sleep(100);
                    RefreshAppointments().ConfigureAwait(false);
                    Logger.Debug($"SubmitClaim: completed, submitted '{procedures.Count}' procedures.");
                }
                return SubmitClaimResult.Ok;
            }
            Logger.Error("Cannot find any procedures for the patient" + " " + patientId);
            return SubmitClaimResult.NoCompletedProcedures;
        }

        private ClaimProcessingStatusResponse GetClaimProcessingStatuses()
        {
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            var address1 = _repository.GetSettingValue<string>(Settings.Address1Key);
            var request = new ClaimProcessingStatusRequest
            {
                TIN = tin,
                State = state,
                Address = address1
            };
            var statuses = _chewsiApi.GetClaimProcessingStatus(request);
            if (statuses != null)
            {
                // set provider id
                statuses.ForEach(m =>
                {
                    if (!string.IsNullOrEmpty(m.PMS_ID))
                    {
                        var appt = _dentalApi.GetAppointmentById(m.PMS_ID);
                        m.ProviderId = appt?.ProviderId;                        
                    }
                });
                return statuses;
            }
            return null;
        }

        public void Dispose()
        {
            _loadTokenSource.Cancel();
            _dentalApi?.Unload();
        }

        private async Task<bool> RefreshAppointments()
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
                                var list = LoadAppointments();
                                Logger.Debug("Broadcasting {0} new claims", list.Count);
                                _clientCallbackService.SetClaims(list);
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
                // Refreshing appointments every 10 seconds
                Utils.SleepWithCancellation(_loadTokenSource.Token, LoadIntervalMs);

                await RefreshAppointments().ConfigureAwait(false);
            }
        }

        private void SetAppointmentState(string id, AppointmentState? state = null, string statusText = null)
        {
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    if (state.HasValue && existing.State != state.Value)
                    {
                        existing.State = state.Value;
                    }
                    if (statusText != null && existing.StatusText != statusText)
                    {
                        existing.StatusText = statusText;
                    }
                    _repository.UpdateAppointment(existing);
                }
            }
        }

        private static class StatusMessage
        {
            public const string PaymentProcessing = "Payment processing...";
            public const string PaymentProcessingError = "Payment processing failed: invalid server response";
            public const string ReadyToSubmit = "Please submit this claim..";
            public const string NoCompletedProcedures = "No completed procedure";
            public const string PatientNotFound = "Cannot find patient in PMS";
            public const string ProviderNotFound = "Cannot find provider in PMS";
            public const string ClaimNotFound = "Cannot find claim";
        }

        public SubmitClaimResult ValidateAndSubmitClaim(string id, double downPayment, int numberOfPayments)
        {
            SubmitClaimResult result = SubmitClaimResult.Error;
            
            lock (_appointmentsLockObject)
            {
                var claim = _repository.GetAppointmentById(id);
                if (claim == null)
                {
                    throw new FaultException("Cannot find claim");
                }

                _clientCallbackService.LockClaim(id);
                Logger.Debug("ValidateAndSubmitClaim: claim {0} has been locked", id);

                SetAppointmentState(id, AppointmentState.TreatmentCompleted, StatusMessage.PaymentProcessing);
                ValidateSubscriberAndProviderResponse validationResponse;
                ProviderInformation providerInformation;
                SubscriberInformation subscriberInformation;
                var serverCalled = ValidateClaim(id, claim.PatientId, out providerInformation, out subscriberInformation, out validationResponse);
                Logger.Debug("ValidateAndSubmitClaim: claim {0} validated with result: {1}, {2}", id, serverCalled, validationResponse.ValidationPassed);
                switch (serverCalled)
                {
                    case ClaimValidationCalled.Success:
                        // got response from Chewsi API, analyze it
                        if (validationResponse.ValidationPassed)
                        {
                            providerInformation.Id = validationResponse.ProviderID;
                            providerInformation.OfficeNbr = validationResponse.OfficeNumber;
                            result = SubmitClaim(id, claim.DateTime, claim.PatientId, providerInformation, subscriberInformation,
                                claim.PmsModifiedDate, downPayment, numberOfPayments);
                            if (result == SubmitClaimResult.NoCompletedProcedures)
                            {
                                SetAppointmentState(id, null, StatusMessage.NoCompletedProcedures);
                            }
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
                        SetAppointmentState(id, AppointmentState.ValidationError, StatusMessage.PatientNotFound);
                        break;
                    case ClaimValidationCalled.PatientNotFound:
                        SetAppointmentState(id, AppointmentState.ValidationError, StatusMessage.ProviderNotFound);
                        break;
                    case ClaimValidationCalled.ClaimNotFound:
                        SetAppointmentState(id, AppointmentState.ValidationError, StatusMessage.ClaimNotFound);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                RefreshAppointments().ConfigureAwait(false);
                _clientCallbackService.UnlockClaim(id);
                return result;
            }
        }

        public SettingsDto GetSettings()
        {
            Logger.Info("Client '{0}' requested settings", GetChannelId());
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
            var city = _repository.GetSettingValue<string>(Settings.City);
            var zip = _repository.GetSettingValue<string>(Settings.Zip);
            return new SettingsDto(pmsType, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state, startPms, machineId, city, zip);
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
            Logger.Info("Client '{0}' saved settings", GetChannelId());
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

                _repository.SaveSetting(Settings.City, settingsDto.City);
                _repository.SaveSetting(Settings.Zip, settingsDto.Zip);
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
                        if ((pmsVersion != pmsVersionOld) || pmsTypeOld != settingsDto.PmsType || address1Old != settingsDto.Address1 
                            || address2Old != settingsDto.Address2 || stateOld != settingsDto.State || osOld != Utils.GetOperatingSystemInfo() 
                            || tinOld != settingsDto.Tin || pluginVersionOld != Utils.GetPluginVersion())
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

        private void DeleteOldRecords()
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

                var procedures = _repository.GetSubmittedProcedures();
                var ids = procedures.Where(m => m.Date < date).Select(m => m.Id).ToList();
                if (ids.Any())
                {
                    Logger.Info("Deleting {0} old submitted procedures", ids.Count);
                    _repository.BulkDeleteSubmittedProcedures(ids);
                }

                var statuses = _repository.GetDeletedStatuses();
                var ids2 = statuses.Where(m => m.Date < date).Select(m => m.Id).ToList();
                if (ids2.Any())
                {
                    Logger.Info("Deleting {0} old deleted statuses", ids2.Count);
                    _repository.BulkDeleteDeletedStatuses(ids2);
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
            Logger.Info("Client '{0}' deleted appointment {1}", GetChannelId(), id);
            bool deleted = false;
            lock (_appointmentsLockObject)
            {
                var existing = _repository.GetAppointmentById(id);
                if (existing != null)
                {
                    var tin = _repository.GetSettingValue<string>(Settings.TIN);
                    if (_chewsiApi.StorePluginClientRowStatus(new PluginClientRowStatus
                    {
                        TIN = tin,
                        Status = PluginClientRowStatus.Statuses.D.ToString(),
                        PMSModifiedDate = existing.PmsModifiedDate.FormatForApiRequest(),
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
                RefreshAppointments().ConfigureAwait(false);
            }
            return deleted;
        }

        private string GetChannelId()
        {
            return OperationContext.Current?.Channel?.SessionId ?? "unknown";
        }

        public bool DeleteClaimStatus(string providerId, string chewsiId, DateTime date)
        {
            Logger.Info("Client '{0}' deleted claim status ({1},{2},{3})", GetChannelId(), providerId, chewsiId, date);
            bool deleted = false;
            lock (_appointmentsLockObject)
            {
                if (!_repository.DeletedStatusExists(providerId, chewsiId, date))
                {
                    _repository.AddDeletedStatus(providerId, chewsiId, date);
                    deleted = true;
                }
            }

            if (deleted)
            {
                RefreshAppointments().ConfigureAwait(false);
            }
            return deleted;
        }

        /// <summary>
        /// Loads appointments from PMS, loads claim statuses from the service, caches them in local DB
        /// </summary>
        private List<ClaimDto> LoadAppointments()
        {
            Logger.Info("Loading appointments");
            try
            {
                // load cached in repository items
                var cached = _repository.GetAppointments();

                #region PMS

                var pms = GetDentalApi().GetAppointments(DateTime.Today);

                pms.ForEach(m => Logger.Debug($"Loaded appointment from PMS: Date={m.Date}, Id={m.Id}, ChewsiId={m.ChewsiId}"));
                cached.ForEach(m => Logger.Debug($"Cached appointment: Date={m.DateTime}, Id={m.Id}, ChewsiId={m.ChewsiId}"));

                // load subscribers' first names (they are displayed if validation fails)
                var subscriberNames =
                    pms.Select(m => m.PatientId)
                        .Distinct()
                        .Select(m => new
                        {
                            info = GetDentalApi().GetPatientInfo(m),
                            patientId = m
                        })
                        .GroupBy(m => m.patientId)
                        .ToDictionary(m => m.Key, m => m.First().info.SubscriberFirstName);
                //subscriberNames.Keys.ToList().ForEach(m => Logger.Debug($"Found subscriber name {subscriberNames[m]} for patient {m}"));
                bool repositoryUpdated = false;

                // merge PMS items with cached in repository

                // add new or update an appointments in repository, exist in PMS, not in the DB
                foreach (var item in pms)
                {
                    var existing = _repository.GetAppointmentById(item.Id);
                    if (existing != null)
                    {
                        var i = new Appointment
                        {
                            Id = item.Id,
                            DateTime = item.Date,
                            PmsModifiedDate = item.PmsModifiedDate,
                            ChewsiId = item.ChewsiId,
                            PatientId = item.PatientId,
                            PatientName = item.PatientName,
                            ProviderId = item.ProviderId,
                            SubscriberFirstName = subscriberNames[item.PatientId],

                            State = existing.State,
                            StatusText = existing.StatusText,
                            FirstMonthlyPaymentDate = existing.FirstMonthlyPaymentDate,
                            DownPayment = existing.DownPayment,
                            NumberOfPayments = existing.NumberOfPayments
                        };
                        if (existing.DateTime != i.DateTime
                            || existing.PmsModifiedDate != i.PmsModifiedDate
                            || existing.ChewsiId != i.ChewsiId
                            || existing.PatientId != i.PatientId
                            || existing.PatientName != i.PatientName
                            || existing.ProviderId != i.ProviderId
                            || existing.SubscriberFirstName != i.SubscriberFirstName
                            || existing.State != i.State
                            || existing.FirstMonthlyPaymentDate != i.FirstMonthlyPaymentDate
                            || !Utils.AreAmountsEqual(existing.DownPayment, i.DownPayment)
                            || existing.NumberOfPayments != i.NumberOfPayments
                            )
                        {
                            Logger.Debug($"Updating PMS appointment in the cache. Id={item.Id}");
                            _repository.UpdateAppointment(i);
                            repositoryUpdated = true;
                        }
                    }
                    else
                    {
                        Logger.Debug($"Adding new PMS appointment into the cache. Id={item.Id}");
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
                            SubscriberFirstName = subscriberNames[item.PatientId],
                            FirstMonthlyPaymentDate = DateTime.Today.AddDays(30),
                            DownPayment = 0,
                            NumberOfPayments = 1
                        });
                        repositoryUpdated = true;                        
                    }
                }
                
                // remove old, exist in the DB, not in PMS
                var old = (from item in cached where pms.All(m => item.Id != m.Id) select item.Id).ToList();
                if (old.Any())
                {
                    Logger.Info("Deleting cached appointments, not found in PMS. Ids={0}", string.Join(",", old));
                    _repository.BulkDeleteAppointments(old);
                    repositoryUpdated = true;
                }

                if (repositoryUpdated)
                {
                    // re-read list from repository
                    cached = _repository.GetAppointments();
                }

                var procedures = cached.Select(m => new
                {
                    id = m.Id,
                    procedures = _dentalApi.GetProcedures(m.PatientId, m.Id, m.DateTime)
                })
                    .ToDictionary(m => m.id, m => m.procedures.Select(p => p.Code).ToList());

                #endregion

                #region Chewsi claims: row statuses

                // load statuses from Chewsi API service
                List<PluginClientRowStatus> clientRowStatuses = new List<PluginClientRowStatus>();
                var tin = _repository.GetSettingValue<string>(Settings.TIN);
                clientRowStatuses.AddRange(_chewsiApi.RetrievePluginClientRowStatuses(tin));

                #endregion

                #region Chewsi claims: processing statuses

                List<ClaimDto> serviceClaims = new List<ClaimDto>();                    
                var statuses = GetClaimProcessingStatuses();                   
                if (statuses != null)
                {
                    var deletedStatuses = _repository.GetDeletedStatuses();
                    foreach (var item in statuses.Where(m => !deletedStatuses.Any(d => d.ChewsiId == m.ChewsiID 
                                                                                        && d.Date.Date == m.PostedOnDate.Date
                                                                                        && d.ProviderId == m.ProviderId)))
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

                        serviceClaims.Add(new ClaimDto
                        {
                            //Id = item.Id,
                            ProviderId = item.ProviderId,
                            Date = item.PostedOnDate,
                            PatientName =
                                string.IsNullOrEmpty(item.PatientLastName)
                                    ? item.PatientFirstName
                                    : $"{item.PatientLastName}, {item.PatientFirstName}",
                            ChewsiId = item.ChewsiID,
                            State = state,
                            StatusText = item.MessageToDisplay,
                            //PatientId = item.PatientId,
                            SubscriberFirstName = item.SubscriberFirstName,
                            IsClaimStatus = true,
                            ClaimNumber = item.Claim_Nbr
                        });
                    }
                }

                #endregion

                var result = (from c in cached
                    where
                        c.State != AppointmentState.Deleted &&
                        c.State != AppointmentState.ValidationCompletedAndClaimSubmitted
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
                        DownPayment = c.DownPayment,
                        FirstMonthlyPaymentDate = c.FirstMonthlyPaymentDate,
                        NumberOfPayments = c.NumberOfPayments,
                        EligibleForPayments = procedures[c.Id] != null && procedures[c.Id].Any(a => ProceduresForPayments.Any(p => p == a))
                        //ClaimNumber = c.ClaimNumber
                    })
                    // don't return deleted and submitted claims
                    .Where(m => !clientRowStatuses.Any(n => Utils.ArePmsModifiedDatesEqual(DateTime.Parse(n.PMSModifiedDate), m.PmsModifiedDate) && n.PMSClaimNbr == m.Id))
                    .Concat(serviceClaims)
                    .OrderByDescending(m => m.State == AppointmentState.TreatmentCompleted || m.IsCptError)
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