using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.ViewModels;
using GalaSoft.MvvmLight;
using NLog;
using IServerAppService = ChewsiPlugin.Api.Interfaces.IServerAppService;

namespace ChewsiPlugin.UI.Services
{
    internal class ClientAppService : ViewModelBase, IClientAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IServerAppService _serverAppService;
        private readonly IDialogService _dialogService;
        private readonly IChewsiApi _chewsiApi;
        private readonly ISettingsViewModel _settingsViewModel;
        private readonly IRepository _repository;
        private readonly bool _isClient;
        private DuplexChannelFactory<IServerAppService> _factory;
        private readonly ServiceDiscovery _serviceDiscovery;

        /// <summary>
        /// Display settings view; try to fill Address, State and TIN
        /// </summary>
        private void OpenSettingsForReview()
        {
            Logger.Info("App first run: setup settings");
            Task.Factory.StartNew(() =>
            {
                try
                {
                    // wait till appointments list is ready
                    /*while (!_appointmentsLoaded || IsLoadingAppointments)
                    {
                        Thread.Sleep(200);
                    }*/

                    // try to find Address, State and TIN in PMS
                    _dialogService.ShowLoadingIndicator();

                    var claim = ClaimItems.FirstOrDefault();
                    var providerId = claim?.ProviderId;
                    if (providerId != null)
                    {
                        Provider provider = GetDentalApi().GetProvider(providerId);
                        if (provider != null)
                        {
                            _settingsViewModel.Show(() => { });
                            _settingsViewModel.Fill(provider.AddressLine1, provider.AddressLine2, provider.State, provider.Tin, true, "localhost", 8888);
                        }
                    }
                }
                finally
                {
                    _dialogService.HideLoadingIndicator();
                }
            });
        }

        public ClientAppService(IDialogService dialogService, IChewsiApi chewsiApi, ISettingsViewModel settingsViewModel, IRepository repository)
        {
            _dialogService = dialogService;
            _chewsiApi = chewsiApi;
            _settingsViewModel = settingsViewModel;
            _repository = repository;
            //TODO
            _isClient = true;//repository.GetSettingValue<bool>(Settings.IsClient);
            _serviceDiscovery = new ServiceDiscovery();
            ClaimItems = new ObservableCollection<ClaimItemViewModel>();
        }

        public void ValidateAndSubmitClaim(string appointmentId, DateTime date, string providerId, string patientId, DateTime pmsModifiedDate)
        {
            _serverAppService.ValidateAndSubmitClaim(appointmentId, date, providerId, patientId, pmsModifiedDate);
        }

        public void DownloadFile(string documentId, string postedDate, bool downloadReport)
        {
            _dialogService.ShowLoadingIndicator();
            try
            {
                var settings = _serverAppService.GetSettings();
                var tin = settings.Tin;
                var stream = _chewsiApi.DownloadFile(new DownoadFileRequest
                {
                    DocumentType = downloadReport ? DownoadFileType.Pdf : DownoadFileType.Txt,
                    TIN = tin,
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
                var settings = _serverAppService.GetSettings();
                var list = _chewsiApi.Get835Downloads(new Request835Downloads
                {
                    TIN = settings.Tin,
                    State = settings.State,
                    Address = $"{settings.Address1} {settings.Address2}"
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

        public bool Initialized { get; }
        public bool IsLoadingAppointments { get; set; }

        public void OpenSettings()
        {
            _settingsViewModel.Show(() =>
            {
                //RefreshAppointments(true, true);
            });
        }

        private void StartAnnouncementService()
        {
            var service = new AnnouncementService();
            // Subscribe the announcement events  
            service.OnlineAnnouncementReceived += OnOnlineEvent;
            service.OfflineAnnouncementReceived += OnOfflineEvent;

            // Create ServiceHost for the AnnouncementService
            ServiceHost serviceHost = new ServiceHost(service);
            // Listen for the announcements sent over UDP multicast  
            serviceHost.AddServiceEndpoint(new UdpAnnouncementEndpoint());
            serviceHost.Open();
        }

        private async Task<bool> FindServerAndInitChannelAsync()
        {
            _dialogService.ShowLoadingIndicator("Connecting...");

            // Get cached server address
            var serverAddress = _repository.GetSettingValue<string>(Settings.ServerAddress);
            // Try to connect using cached address
            var connected = Connect(serverAddress);
            if (!connected)
            {
                // Find server by service discovery
                var address = await _serviceDiscovery.Discover();
                connected = Connect(address?.Uri.ToString());
                if (connected)
                {
                    _repository.SaveSetting(Settings.ServerAddress, address);
                }
            }
            _dialogService.HideLoadingIndicator();
            return connected;
        }

        private bool Connect(string serverAddress)
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
            InstanceContext instanceContext = new InstanceContext(new CallbackHandler(_dialogService));
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

        public async void Initialize()
        {
            _dialogService.ShowLoadingIndicator("Initializing...");
            StartAnnouncementService();
            if (_isClient)
            {
                if (await FindServerAndInitChannelAsync())
                {
                    if (_serverAppService.InitClient())
                    {
                        var settings = _serverAppService.GetSettings();
                        _settingsViewModel.InjectAppServiceAndInit(this, settings);
                        InitializeChewsiApi(settings);  
                          
                        // load appts

                    }
                }
                else
                {
                    _dialogService.Show("Failed to connect to Chewsi service.", "Error", async () =>
                    {
                        await FindServerAndInitChannelAsync();
                    }, "Try Again");
                }
            }
        }

        private void InitializeChewsiApi(SettingsDto settings)
        {
            _chewsiApi.Initialize(settings.MachineId, settings.UseProxy, settings.ProxyAddress, settings.ProxyPort, settings.ProxyLogin, settings.ProxyPassword);
        }

        public void DeleteAppointment(string id)
        {
            _serverAppService.DeleteAppointment(id);
        }

        public void RefreshAppointments()
        {
            _serverAppService.RefreshAppointments(true, true);
        }

        public void SaveSettings(SettingsDto settingsDto)
        {
            _serverAppService.SaveSettings(settingsDto);
        }

        private IDentalApi GetDentalApi()
        {
            return null;
        }

        private void OnOnlineEvent(object sender, AnnouncementEventArgs e)
        {
            CreateChannel();
        }

        private void OnOfflineEvent(object sender, AnnouncementEventArgs e)
        {
            ChannelFaulted(_serverAppService, null);
        }
    }
}
