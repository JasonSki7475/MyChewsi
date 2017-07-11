using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Models;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Threading;
using NLog;
using IServerAppService = ChewsiPlugin.Api.Interfaces.IServerAppService;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace ChewsiPlugin.UI.Services
{
    internal class ClientAppService : ViewModelBase, IClientAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _appointmentsLockObject = new object();
        private const int ServiceReadyTimeoutMs = 120000;

        private IServerAppService _serverAppService;
        private readonly IClientDialogService _dialogService;
        private readonly ISettingsViewModel _settingsViewModel;
        private readonly IRepository _repository;
        private readonly IConnectViewModel _connectViewModel;
        private readonly ILauncherService _launcherService;
        private readonly IPaymentsCalculationViewModel _paymentsCalculationViewModel;
        private DuplexChannelFactory<IServerAppService> _factory;
        private ClientState _state;
        private bool _isLoadingClaims;
        private ServiceHost _announcementServiceHost;
        private readonly bool _isClient;

        public ClientAppService(IClientDialogService dialogService, ISettingsViewModel settingsViewModel, 
            IRepository repository, IConnectViewModel connectViewModel, ILauncherService launcherService, IPaymentsCalculationViewModel paymentsCalculationViewModel)
        {
            SetState(ClientState.Initializing);
            _dialogService = dialogService;
            _settingsViewModel = settingsViewModel;
            _repository = repository;
            _connectViewModel = connectViewModel;
            _launcherService = launcherService;
            _paymentsCalculationViewModel = paymentsCalculationViewModel;
            _connectViewModel.InjectAppServiceAndInit(this);
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
            _isClient = _repository.GetSettingValue<bool>(Settings.IsClient);
        }

        public void Initialize()
        {
            StartService();
            SetState(ClientState.Initializing);
            _dialogService.ShowLoadingIndicator("Initializing client");
            StartAnnouncementService();
            Connect();
        }

        public string Title => _isClient ? "Chewsi Client" : "Chewsi Server";

        private void StartService()
        {
            var isClient = _repository.GetSettingValue<bool>(Settings.IsClient);
            if (!isClient)
            {
                try
                {
                    ServiceController sc = new ServiceController
                    {
                        ServiceName = "ChewsiService"
                    };
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        Logger.Info("This is server installation, service is not running, starting the service");
                        _dialogService.ShowLoadingIndicator("Starting the Chewsi service...");

                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running);
                        Logger.Info("Service start complete, status: " + sc.Status);
                    }
                }
                catch (InvalidOperationException e)
                {
                    Logger.Debug(e, "Failed to get status and start service");
                }
            }
        }

        public void ValidateAndSubmitClaim(string id, double downPayment, int numberOfPayments)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                try
                {
                    LockClaim(id);
                    _dialogService.ShowLoadingIndicator("Submitting...");
                    SubmitClaimResult result;
                    if (Api.Common.Utils.TrySafeCall(() => _serverAppService.ValidateAndSubmitClaim(id, downPayment, numberOfPayments), out result))
                    {
                        switch (result)
                        {
                            case SubmitClaimResult.Error:
                                //_dialogService.Show("Cannot submit claim, error occured. Please try again.", "Error");
                                break;
                            case SubmitClaimResult.AlreadyDeleted:
                                _dialogService.Show("The claim has already been deleted", "Error");
                                break;
                            case SubmitClaimResult.AlreadySubmitted:
                                _dialogService.Show("The claim has already been submitted", "Error");
                                break;
                            case SubmitClaimResult.NoCompletedProcedures:
                                _dialogService.Show("The claim has no completed procedures", "Error");
                                break;
                            case SubmitClaimResult.Ok:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }                        
                    }
                    else
                    {
                        _dialogService.Show("Cannot submit claim, communication error occured. Please try again.", "Error");
                    }
                }
                finally
                {
                    _dialogService.HideLoadingIndicator();
                }
            };
            worker.RunWorkerAsync();
        }

        public void DownloadFile(string documentId, string postedDate, bool downloadReport)
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                File835Dto response;
                if (Api.Common.Utils.TrySafeCall(() => _serverAppService.DownloadFile(downloadReport ? DownoadFileType.Pdf : DownoadFileType.Txt,
                        documentId, postedDate), out response) && response != null && response.Content != null &&
                    response.Content.Length > 0)
                {
                    var dialog = new SaveFileDialog
                    {
                        FileName =
                            downloadReport
                                ? $"Report_{postedDate.Replace('/', '-')}.pdf"
                                : $"EDI_{postedDate.Replace('/', '-')}.txt",
                        Title = downloadReport ? "Save report file" : "Save 835 file",
                        Filter = downloadReport ? "PDF file (*.pdf)|*.pdf" : "Text file (*.txt)|*.txt"
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {

                        using (FileStream file = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write))
                        {
                            file.Write(response.Content, 0, response.Content.Length);
                        }
                        _dialogService.Show("File successfully downloaded", "Information");
                    }
                }
                else
                {
                    _dialogService.Show("Error downloading file. Try again later.", "Error");
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public List<DownloadItemViewModel> GetDownloads()
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                List<DownloadDto> list;
                if (Api.Common.Utils.TrySafeCall(() => _serverAppService.GetDownloads(), out list))
                {
                    return list.Select(m => new DownloadItemViewModel(m.Edi, m.Report, m.Status, m.PostedDate)).ToList();
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
            return new List<DownloadItemViewModel>();
        }

        public List<PaymentPlanHistoryViewModel> GetPayments()
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                List<PaymentPlanHistoryDto> list;
                if (Api.Common.Utils.TrySafeCall(() => _serverAppService.GetPayments(), out list))
                {
                    return list.Select(m => new PaymentPlanHistoryViewModel(m.ChewsiId, m.Items, m.LastPaymentOn, 
                        m.PatientFirstName, m.PaymentSchedule, m.PostedOn, m.Provider, m.BalanceRemaining, m.NextPaymentOn)).ToList();
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
            return new List<PaymentPlanHistoryViewModel>();            
        }

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; }

        public bool Initialized => _state == ClientState.Ready;

        private void SetState(ClientState state)
        {
            _state = state;
            RaiseInitGetter();
        }

        public bool IsLoadingClaims
        {
            get { return _isLoadingClaims; }
            private set
            {
                _isLoadingClaims = value;
                RaisePropertyChanged(() => IsLoadingClaims);
            }
        }

        public void OpenSettings()
        {
            _settingsViewModel.Show(async () =>
            {
                if (!_isClient)
                {
                    await ReloadClaims().ConfigureAwait(false);
                }
                SetState(ClientState.Ready);
                RaiseInitGetter();
            });
        }

        private void RaiseInitGetter()
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                RaisePropertyChanged(() => Initialized);
            });
        }

        private void StartAnnouncementService()
        {
            Logger.Info("Starting announcement service");
            var service = new AnnouncementService();
            // Subscribe the announcement events  
            service.OnlineAnnouncementReceived += OnOnlineEvent;
            service.OfflineAnnouncementReceived += OnOfflineEvent;

            // Create ServiceHost for the AnnouncementService
            _announcementServiceHost = new ServiceHost(service);
            // Listen for the announcements sent over UDP multicast  
            _announcementServiceHost.AddServiceEndpoint(new UdpAnnouncementEndpoint());
            _announcementServiceHost.Open();
            Logger.Info("Announcement service started");
        }
        
        private void CreateChannel()
        {
            _serverAppService = _factory.CreateChannel();
            (_serverAppService as ICommunicationObject).Faulted += ChannelFaulted;
        }

        private DuplexChannelFactory<IServerAppService> GetFactory(EndpointAddress address)
        {
            InstanceContext instanceContext = new InstanceContext(new CallbackHandler(_dialogService, this));
            return new DuplexChannelFactory<IServerAppService>(instanceContext, new WSDualHttpBinding
            {
                Security = new WSDualHttpSecurity
                {
                    Mode = WSDualHttpSecurityMode.None
                },
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue
            }, address);
        }

        private void ChannelFaulted(object sender, EventArgs e)
        {
            _dialogService.ShowLoadingIndicator("Disconnected from the service. Reconnecting...");
            Logger.Warn("Disconnected from the service");
            var co = (ICommunicationObject) sender;
            co.Faulted -= ChannelFaulted;
            co.Abort();
            CreateChannel();
            _dialogService.HideLoadingIndicator();
        }

        public async Task<bool> Connect(string serverAddress = null)
        {
            _dialogService.ShowLoadingIndicator("Connecting...");
            bool result = false;
            if (serverAddress == null)
            {
                // Get cached server address
                serverAddress = _repository.GetSettingValue<string>(Settings.ServerAddress);
            }
            if (serverAddress == null && !_isClient)
            {
                serverAddress = Api.Common.Utils.GetAddressFromHost("localhost");
            }
            Logger.Info("Connecting to {0}", serverAddress);

            if (serverAddress != null)
            {
                _factory = GetFactory(new EndpointAddress(serverAddress));
                CreateChannel();

                // Try to connect
                bool response;
                if (Api.Common.Utils.TrySafeCall(() => _serverAppService.Ping(), out response) && response)
                {
                    Logger.Info("Server responded to ping request, initializing client");
                    _repository.SaveSetting(Settings.ServerAddress, serverAddress);

                    ServerState serverState;
                    if (Api.Common.Utils.TrySafeCall(() => _serverAppService.InitClient(), out serverState))
                    {
                        Logger.Info("Service state is '{0}'", serverState);
                        switch (serverState)
                        {
                            case ServerState.Initializing:
                                // Wait 120s while service is starting
                                var startTime = DateTime.UtcNow;
                                _dialogService.ShowLoadingIndicator("Waiting while service is starting...");
                                while ((DateTime.UtcNow - startTime).TotalMilliseconds < ServiceReadyTimeoutMs)
                                {
                                    await TaskEx.Delay(1000);
                                    if (Api.Common.Utils.TrySafeCall(() => _serverAppService.InitClient(), out serverState) && serverState != ServerState.Initializing)
                                    {
                                        break;
                                    }
                                }
                                _dialogService.HideLoadingIndicator();
                                if (serverState == ServerState.Initializing)
                                {
                                    _dialogService.HideLoadingIndicator();
                                    _dialogService.Show("Cannot load data from the Chewsi plugin's service. Server is not ready.", "Error");
                                }
                                else
                                {
                                    return await Connect(serverAddress).ConfigureAwait(false);
                                }
                                break;
                            case ServerState.Ready:
                                SettingsDto settings;
                                if (Api.Common.Utils.TrySafeCall(_serverAppService.GetSettings, out settings) && settings != null)
                                {
                                    RaisePropertyChanged(() => Title);
                                    _settingsViewModel.InjectAppServiceAndInit(this, settings, serverAddress, _launcherService.GetLauncherStartup(), _isClient);
                                    await ReloadClaims().ConfigureAwait(false);
                                    SetState(ClientState.Ready);
                                    RaiseInitGetter();
                                    result = true;

                                    // Start PMS
                                    if (settings.StartPms)
                                    {
                                        string pmsExePath;
                                        if (Api.Common.Utils.TrySafeCall(_serverAppService.GetPmsExecutablePath, out pmsExePath) && pmsExePath != null)
                                        {
                                            Logger.Info("Starting PMS: {0}", pmsExePath);
                                            _launcherService.StartPms(pmsExePath);
                                        }                                        
                                    }
                                }
                                else
                                {
                                    await TaskEx.Delay(2000);
                                    await Connect(serverAddress);
                                }
                                break;
                            case ServerState.InvalidSettings:
                                OpenSettingsForReview(serverAddress);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    else
                    {
                        await TaskEx.Delay(2000);
                        await Connect(serverAddress).ConfigureAwait(false);
                    }
                }
                else
                {
                    Logger.Warn("Server didn't respond to ping request");
                }        
            }
            else
            {
                Logger.Warn("Server address is null, showing 'Connection' view");
                serverAddress = Api.Common.Utils.GetAddressFromHost("localhost");
                _connectViewModel.Show(serverAddress);
            }
            _dialogService.HideLoadingIndicator();
            return result;
        }

        /// <summary>
        /// Display settings view; try to fill Address, State and TIN
        /// </summary>
        private void OpenSettingsForReview(string serverAddress)
        {
            Logger.Info("App first run: setup settings");
            _dialogService.ShowLoadingIndicator("Loading settings...");
            InitialSettingsDto s;
            if (Api.Common.Utils.TrySafeCall(_serverAppService.GetInitialSettings, out s) && s != null)
            {
                var settings = new SettingsDto(s.PmsType, s.AddressLine1, s.AddressLine2, s.Tin, true, "localhost", 8888, "", "", s.State, false, "", s.City, s.Zip);
                RaisePropertyChanged(() => Title);
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    _settingsViewModel.InjectAppServiceAndInit(this, settings, serverAddress, _launcherService.GetLauncherStartup(), _isClient);
                    OpenSettings();
                });                
            }
            else
            {
                _dialogService.Show("Cannot connect to Chewsi service. Please restart your computer.", "Error");
            }
            _dialogService.HideLoadingIndicator();
        }
        
        public void DeleteAppointment(string id)
        {
            bool result;
            if (!Api.Common.Utils.TrySafeCall(() => _serverAppService.DeleteAppointment(id), out result) || !result)
            {
                _dialogService.Show("Cannot delete claim, error occured. Please try again.", "Error");
            }
        }   
             
        public void DeleteClaimStatus(string providerId, string chewsiId, DateTime date)
        {
            bool result;
            if (!Api.Common.Utils.TrySafeCall(() => _serverAppService.DeleteClaimStatus(providerId, chewsiId, date), out result) || !result)
            {
                _dialogService.Show("Cannot delete claim status, error occured. Please try again.", "Error");
            }
        }

        public CalculatedPaymentsDto GetCalculatedPayments(string id, double downPayment, int numberOfPayments,
            DateTime firstMonthlyPaymentDate)
        {
            _dialogService.ShowLoadingIndicator();
            CalculatedPaymentsDto result;
            if (!Api.Common.Utils.TrySafeCall(() => _serverAppService.GetCalculatedPayments(id, downPayment, numberOfPayments, firstMonthlyPaymentDate), out result) || result == null)
            {
                _dialogService.Show("Cannot calculate payments", "Error");
            }
            _dialogService.HideLoadingIndicator();
            return result;
        }

        public async void SaveSettings(SettingsDto settingsDto, string serverAddress, bool startLauncher)
        {
            bool result;
            var called = Api.Common.Utils.TrySafeCall(() => _serverAppService.SaveSettings(settingsDto), out result);
            if (called)
            {
                if (result)
                {
                    // start or kill launcher
                    var currentLaunchSetting = _launcherService.GetLauncherStartup();
                    if (currentLaunchSetting ^ startLauncher)
                    {
                        if (startLauncher)
                        {
                            _launcherService.StartLauncher();
                        }
                        else
                        {
                            _launcherService.KillLauncher();
                        }
                    }
                    _launcherService.SetLauncherStartup(startLauncher);                    
                }
                else
                {
                    _dialogService.Show("Some settings are incorrect or empty. Please enter again.", "Error");
                }
            }
            else
            {
                _dialogService.Show("Cannot save settings, communication error occured. Please try again.", "Error");
            }

            await Connect(serverAddress);
        }

        private async void OnOnlineEvent(object sender, AnnouncementEventArgs e)
        {
            var state = (_serverAppService as ICommunicationObject).State;
            if (state != CommunicationState.Opened && state != CommunicationState.Opening)
            {
                await Connect(e.EndpointDiscoveryMetadata.Address.Uri.ToString()).ConfigureAwait(false);
            }
        }

        private async void OnOfflineEvent(object sender, AnnouncementEventArgs e)
        {
            SetState(ClientState.Initializing);
            await Connect(e.EndpointDiscoveryMetadata.Address.Uri.ToString()).ConfigureAwait(false);
        }

        public void LockClaim(string id)
        {
            var claim = ClaimItems.FirstOrDefault(m => id == m.Id);
            claim?.Lock();
        }

        public void UnlockClaim(string id)
        {
            var claim = ClaimItems.FirstOrDefault(m => id == m.Id);
            claim?.Unlock();
        }
        
        public async Task<bool> ReloadClaims()
        {
            return await Task<bool>.Factory.StartNew(() =>
            {
                if (!IsLoadingClaims)
                {
                    lock (_appointmentsLockObject)
                    {
                        if (!IsLoadingClaims)
                        {
                            IsLoadingClaims = true;
                            _dialogService.ShowLoadingIndicator("Loading...");
                            try
                            {
                                if (Api.Common.Utils.SafeCall(_serverAppService.ReloadClaims))
                                {
                                    return true;
                                }
                                else
                                {
                                    _dialogService.Show("Cannot load appoitments list.", "Error", async () => await ReloadClaims());
                                    return false;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Error loading appointments");
                                return false;
                            }
                            finally
                            {
                                IsLoadingClaims = false;
                            }
                        }
                    }
                }
                return false;
            }).ConfigureAwait(false);
        }

        public void SetClaims(List<ClaimDto> claims)
        {
            _dialogService.ShowLoadingIndicator("Loading...");
            DispatcherHelper.RunAsync(() =>
            {
                // save payment settings
                ClaimItemViewModel [] old = new ClaimItemViewModel[ClaimItems.Count];
                ClaimItems.CopyTo(old, 0);

                ClaimItems.Clear();
                foreach (var c in claims)
                {
                    var oldClaim = old.FirstOrDefault(m => m.Id == c.Id);
                    var numberOfPayments = oldClaim?.NumberOfPayments ?? c.NumberOfPayments;
                    var firstMonthlyPaymentDate = oldClaim?.FirstMonthlyPaymentDate ?? c.FirstMonthlyPaymentDate;
                    var downPayment = oldClaim?.DownPayment ?? c.DownPayment;

                    ClaimItems.Add(new ClaimItemViewModel(this, _paymentsCalculationViewModel)
                    {
                        Id = c.Id,
                        ProviderId = c.ProviderId,
                        Date = c.Date,
                        PatientName = c.PatientName,
                        ChewsiId = c.ChewsiId,
                        State = c.State,
                        StatusText = c.StatusText,
                        PatientId = c.PatientId,
                        SubscriberFirstName = c.SubscriberFirstName,
                        IsClaimStatus = c.IsClaimStatus,
                        IsCptError = c.IsCptError,
                        ClaimNumber = c.ClaimNumber,
                        FirstMonthlyPaymentDate = firstMonthlyPaymentDate,
                        DownPayment = downPayment,
                        NumberOfPayments = numberOfPayments,
                        Locked = false,
                        PmsModifiedDate = c.PmsModifiedDate,
                        EligibleForPayments = c.EligibleForPayments
                    });
                }
                _dialogService.HideLoadingIndicator();
            });
        }

        public void Dispose()
        {
            Logger.Info("Application is shutting down");
            _serverAppService?.DisconnectClient();
            _announcementServiceHost?.Close();
        }
    }
}
