using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.ServiceProcess;
using System.Threading;
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

namespace ChewsiPlugin.UI.Services
{
    internal class ClientAppService : ViewModelBase, IClientAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _appointmentsLockObject = new object();

        private IServerAppService _serverAppService;
        private readonly IClientDialogService _dialogService;
        private readonly IChewsiApi _chewsiApi;
        private readonly ISettingsViewModel _settingsViewModel;
        private readonly IRepository _repository;
        private DuplexChannelFactory<IServerAppService> _factory;
        private readonly ServiceDiscovery _serviceDiscovery;
        private SettingsDto _settings;
        private ClientState _state;
        private bool _isLoadingClaims;
        private ServiceHost _serviceHost;

        public ClientAppService(IClientDialogService dialogService, IChewsiApi chewsiApi, ISettingsViewModel settingsViewModel, IRepository repository)
        {
            //_settings = new SettingsDto(Settings.PMS.Types.Dentrix, "", "", "", "", false, "", 8080, "", "", "", false, false, "", false);
            _state = ClientState.Initializing;
            _dialogService = dialogService;
            _chewsiApi = chewsiApi;
            _settingsViewModel = settingsViewModel;
            _repository = repository;
            _serviceDiscovery = new ServiceDiscovery();
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
        }

        public void Initialize()
        {
            StartService();
            _state = ClientState.Initializing;
            _dialogService.ShowLoadingIndicator("Initializing client");
            StartAnnouncementService();
            Connect();
        }

        private void StartService()
        {
            var isClient = _repository.GetSettingValue<bool>(Settings.IsClient);
            if (!isClient)
            {
                try
                {
                    ServiceController sc = new ServiceController
                    {
                        ServiceName = "Chewsi Service"
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

        public void ValidateAndSubmitClaim(string id)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
            {
                try
                {
                    LockClaim(id);
                    _dialogService.ShowLoadingIndicator("Submitting...");
                    SubmitClaimResult result;
                    if (Utils.TrySafeCall(_serverAppService.ValidateAndSubmitClaim, id, out result))
                    {
                        switch (result)
                        {
                            case SubmitClaimResult.Error:
                                //_dialogService.Show("Cannot submit claim, error occured. Please try again.", "Error");
                                break;
                            case SubmitClaimResult.AlreadyDeleted:
                                _dialogService.Show("Claim has already been deleted", "Error");
                                break;
                            case SubmitClaimResult.AlreadySubmitted:
                                _dialogService.Show("Claim has already been submitted", "Error");
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
                var stream = _chewsiApi.DownloadFile(new DownoadFileRequest
                {
                    DocumentType = downloadReport ? DownoadFileType.Pdf : DownoadFileType.Txt, TIN = _settings.Tin, DocumentID = documentId, PostedOnDate = postedDate
                });
                if (stream != null)
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
                            stream.CopyTo(file);
                            stream.Close();
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
                var list = _chewsiApi.Get835Downloads(new Request835Downloads
                {
                    TIN = _settings.Tin,
                    State = _settings.State,
                    Address = $"{_settings.Address1} {_settings.Address2}"
                }).Select(m => new DownloadItemViewModel(m.EDI_835_EDI, m.EDI_835_Report, m.Status, m.PostedDate)).ToList();
                return list;
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; }

        public bool Initialized => _state == ClientState.Ready;

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
                if (!_settings.IsClient)
                {
                    await ReloadClaims(true);
                }
                _state = ClientState.Ready;
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
            _serviceHost = new ServiceHost(service);
            // Listen for the announcements sent over UDP multicast  
            _serviceHost.AddServiceEndpoint(new UdpAnnouncementEndpoint());
            _serviceHost.Open();
            Logger.Info("Announcement service started");
        }

        private async Task<bool> FindServerAndInitChannelAsync(string serverAddress)
        {
            _dialogService.ShowLoadingIndicator("Connecting...");

            if (serverAddress == null)
            {
                // Get cached server address
                serverAddress = _repository.GetSettingValue<string>(Settings.ServerAddress);
            }

            // Try to connect using cached address
            var connected = TryCreateChannel(serverAddress);
            if (!connected)
            {
                // Find server by service discovery
                var address = await _serviceDiscovery.Discover();
                connected = TryCreateChannel(address?.Uri.ToString());
                if (connected)
                {
                    _repository.SaveSetting(Settings.ServerAddress, address);
                }
            }
            _dialogService.HideLoadingIndicator();
            return connected;
        }

        private bool TryCreateChannel(string serverAddress)
        {
            if (!string.IsNullOrEmpty(serverAddress))
            {
                _factory = GetFactory(new EndpointAddress(serverAddress));
                CreateChannel();
                return true;
            }
            return false;
        }

        private void CreateChannel()
        {
            _serverAppService = _factory.CreateChannel();
            (_serverAppService as ICommunicationObject).Faulted += ChannelFaulted;
        }

        private DuplexChannelFactory<IServerAppService> GetFactory(EndpointAddress address)
        {
            InstanceContext instanceContext = new InstanceContext(new CallbackHandler(_dialogService, this));
            return new DuplexChannelFactory<IServerAppService>(instanceContext, new WSDualHttpBinding(), address);
        }

        private void ChannelFaulted(object sender, EventArgs e)
        {
            Logger.Warn("Disconnected from the service");
            var co = (ICommunicationObject) sender;
            co.Faulted -= ChannelFaulted;
            co.Abort();
            CreateChannel();
        }

        private async void Connect(string serverAddress = null)
        {
            if (await FindServerAndInitChannelAsync(serverAddress))
            {
                ServerState serverState;
                if (Utils.TrySafeCall(() => _serverAppService.InitClient(), out serverState))
                {
                    switch (serverState)
                    {
                        case ServerState.Initializing:
                            _dialogService.HideLoadingIndicator();
                            _dialogService.Show("Cannot load data from the Chewsi plugin's service. Server is not ready.", "Error");
                            break;
                        case ServerState.Ready:
                            SettingsDto settings;
                            if (Utils.TrySafeCall(_serverAppService.GetSettings, out settings) && settings != null)
                            {
                                _settings = settings;
                                _settingsViewModel.InjectAppServiceAndInit(this, settings);
                                InitializeChewsiApi(settings);
                                await ReloadClaims(false);
                                _state = ClientState.Ready;
                                RaiseInitGetter();
                            }
                            else
                            {
                                Thread.Sleep(2000);
                                Connect(serverAddress);
                            }
                            break;
                        case ServerState.InvalidSettings:
                            OpenSettingsForReview();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }                    
                }
                else
                {
                    Thread.Sleep(2000);
                    Connect(serverAddress);
                }
            }
            else
            {
                _dialogService.Show("Failed to connect to Chewsi service.", "Error", () => Connect(), "Try Again");
            }
        }

        /// <summary>
        /// Display settings view; try to fill Address, State and TIN
        /// </summary>
        private void OpenSettingsForReview()
        {
            Logger.Info("App first run: setup settings");
            _dialogService.ShowLoadingIndicator("Loading settings...");
            InitialSettingsDto s;
            if (Utils.TrySafeCall(_serverAppService.GetInitialSettings, out s) && s != null)
            {
                _settings = new SettingsDto(s.PmsType, s.PmsPath, s.AddressLine1, s.AddressLine2, s.Tin, true, "localhost", 8888, "", "", s.State, false, false, "", s.IsClient);
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    _settingsViewModel.InjectAppServiceAndInit(this, _settings);
                    OpenSettings();
                });                
            }
            else
            {
                _dialogService.Show("Cannot connect to Chewsi service. Please restart your computer.", "Error");
            }
            _dialogService.HideLoadingIndicator();
        }

        private void InitializeChewsiApi(SettingsDto settings)
        {
            _chewsiApi.Initialize(settings.MachineId, settings.UseProxy, settings.ProxyAddress, settings.ProxyPort, settings.ProxyLogin, settings.ProxyPassword);
        }

        public void DeleteAppointment(string id)
        {
            bool result;
            if (!Utils.TrySafeCall(_serverAppService.DeleteAppointment, id, out result) || !result)
            {
                _dialogService.Show("Cannot delete claim, error occured. Please try again.", "Error");
            }
        }

        public void SaveSettings(SettingsDto settingsDto)
        {
            bool result;
            var called = Utils.TrySafeCall(_serverAppService.SaveSettings, settingsDto, out result);
            if (!called)
            {
                _dialogService.Show("Cannot save settings, communication error occured. Please try again.", "Error");
            } else if (!result)
            {
                _dialogService.Show("Some settings are incorrect or empty. Please enter again.", "Error");
            }
        }

        private void OnOnlineEvent(object sender, AnnouncementEventArgs e)
        {
            var state = (_serverAppService as ICommunicationObject).State;
            if (state != CommunicationState.Opened && state != CommunicationState.Opening)
            {
                Connect(e.EndpointDiscoveryMetadata.Address.Uri.ToString());
            }
        }

        private void OnOfflineEvent(object sender, AnnouncementEventArgs e)
        {
            ChannelFaulted(_serverAppService, null);
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

        public async Task<bool> ReloadClaims(bool force)
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
                                List<ClaimDto> claims;
                                if (Utils.TrySafeCall(_serverAppService.GetClaims, force, out claims) && claims != null)
                                {
                                    SetClaims(claims);
                                    return true;
                                }
                                else
                                {
                                    _dialogService.Show("Cannot load appoitments list.", "Error", () => ReloadClaims(force));
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
            });
        }

        public void SetClaims(List<ClaimDto> claims)
        {
            _dialogService.ShowLoadingIndicator("Loading...");
            DispatcherHelper.RunAsync(() =>
            {
                ClaimItems.Clear();
                foreach (var c in claims)
                {
                    ClaimItems.Add(new ClaimItemViewModel(this)
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
                        ClaimNumber = c.ClaimNumber,
                        Locked = false,
                        PmsModifiedDate = c.PmsModifiedDate
                    });
                }
                _dialogService.HideLoadingIndicator();
            });
        }

        public void Dispose()
        {
            Logger.Info("Application is shutting down");
            _serverAppService?.DisconnectClient();
        }
    }
}
