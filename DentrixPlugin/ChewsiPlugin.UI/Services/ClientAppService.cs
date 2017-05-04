using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Discovery;
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

        public ClientAppService(IClientDialogService dialogService, IChewsiApi chewsiApi, ISettingsViewModel settingsViewModel, IRepository repository)
        {
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
            _state = ClientState.Initializing;
            _dialogService.ShowLoadingIndicator("Initializing client");
            StartAnnouncementService();
            Connect();
        }

        public void ValidateAndSubmitClaim(string id)
        {
            if (!Call(_serverAppService.ValidateAndSubmitClaim, id))
            {
                _dialogService.Show("Cannot submit claim, communication error occured. Please try again.", "Error");
            }
        }

        public void DownloadFile(string documentId, string postedDate, bool downloadReport)
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                var stream = _chewsiApi.DownloadFile(new DownoadFileRequest
                {
                    DocumentType = downloadReport ? DownoadFileType.Pdf : DownoadFileType.Txt,
                    TIN = _settings.Tin,
                    DocumentID = documentId,
                    PostedOnDate = postedDate
                });
                if (stream != null)
                {
                    var dialog = new SaveFileDialog
                    {
                        FileName = downloadReport ? $"Report_{postedDate.Replace('/', '-')}.pdf" : $"EDI_{postedDate.Replace('/', '-')}.txt",
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
                })
                .Select(m => new DownloadItemViewModel(m.EDI_835_EDI, m.EDI_835_Report, m.Status, m.PostedDate))
                .ToList();
                return list;
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public ObservableCollection<ClaimItemViewModel> ClaimItems { get; }

        public bool Initialized => _state == ClientState.Ready;

        public bool IsLoadingClaims { get; private set; }

        public void OpenSettings()
        {
            _settingsViewModel.Show(() =>
            {
                if (!_settings.IsClient)
                {
                    ReloadClaims(true);
                }
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
            ServiceHost serviceHost = new ServiceHost(service);
            // Listen for the announcements sent over UDP multicast  
            serviceHost.AddServiceEndpoint(new UdpAnnouncementEndpoint());
            serviceHost.Open();
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

        private bool Call<T>(Action<T> action, T arg)
        {
            try
            {
                action.Invoke(arg);
                return true;
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server-side");
                throw;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communications error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return false;
        }
        private T Call<T>(Func<T> action)
        {
            try
            {
                return action.Invoke();
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server-side");
                throw;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communications error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return default(T);
        }
        private R Call<T, R>(Func<T, R> action, T arg)
        {
            try
            {
                return action.Invoke(arg);
            }
            catch (TimeoutException)
            {
                Logger.Warn("Service timeout");
            }
            catch (FaultException ex)
            {
                Logger.Warn(ex, "Exception handled on server-side");
                throw;
            }
            catch (CommunicationException ex)
            {
                Logger.Warn(ex, "Communications error");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected WCF error");
            }
            return default(R);
        }

        private async void Connect(string serverAddress = null)
        {
            if (await FindServerAndInitChannelAsync(serverAddress))
            {
                var serverState = Call(() => _serverAppService.InitClient());
                switch (serverState)
                {
                    case ServerState.Initializing:
                        _dialogService.HideLoadingIndicator();
                        _dialogService.Show("Cannot load data from the Chewsi plugin's service. Server is not ready.", "Error");
                        break;
                    case ServerState.Ready:
                        var settings = Call(_serverAppService.GetSettings);
                        if (settings != null)
                        {
                            _settings = settings;
                            _settingsViewModel.InjectAppServiceAndInit(this, settings);
                            InitializeChewsiApi(settings);
                            ReloadClaims(false);
                            _state = ClientState.Ready;
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
            var initialSettings = Call(_serverAppService.GetInitialSettings);
            OpenSettings();
            if (initialSettings != null)
            {
                _settingsViewModel.Fill(initialSettings.AddressLine1, initialSettings.AddressLine2, initialSettings.State, initialSettings.Tin, true, "localhost", 8888);
            }
            _dialogService.HideLoadingIndicator();
        }

        private void InitializeChewsiApi(SettingsDto settings)
        {
            _chewsiApi.Initialize(settings.MachineId, settings.UseProxy, settings.ProxyAddress, settings.ProxyPort, settings.ProxyLogin, settings.ProxyPassword);
        }

        public void DeleteAppointment(string id)
        {
            if (!Call(_serverAppService.DeleteAppointment, id))
            {
                _dialogService.Show("Cannot delete claim, communication error occured. Please try again.", "Error");
            }
        }

        public void SaveSettings(SettingsDto settingsDto)
        {
            if (!Call(_serverAppService.SaveSettings, settingsDto))
            {
                _dialogService.Show("Cannot save _settings, communication error occured. Please try again.", "Error");
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

        public void ReloadClaims(bool force)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (i, j) =>
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
                                var claims = Call(_serverAppService.GetClaims, force);
                                if (claims != null)
                                {
                                    SetClaims(claims);
                                }
                                else
                                {
                                    _dialogService.Show("Cannot load appoitments list.", "Error", () => ReloadClaims(force));
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Error loading appointments");
                            }
                            finally
                            {
                                IsLoadingClaims = false;
                            }
                        }
                    }
                }
            };
            worker.RunWorkerAsync();
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
                        Id = c.Id, ProviderId = c.ProviderId, Date = c.Date, PatientName = c.PatientName, ChewsiId = c.ChewsiId, State = c.State, StatusText = c.StatusText, PatientId = c.PatientId, SubscriberFirstName = c.SubscriberFirstName, IsClaimStatus = c.IsClaimStatus, ClaimNumber = c.ClaimNumber, Locked = false, PmsModifiedDate = c.PmsModifiedDate
                    });
                }
                _dialogService.HideLoadingIndicator();
            });
        }
    }
}
