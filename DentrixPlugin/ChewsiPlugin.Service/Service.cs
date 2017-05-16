using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Discovery;
using System.ServiceProcess;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.Service.Services;

namespace ChewsiPlugin.Service
{
    public partial class Service : ServiceBase
    {
        private const string Address = "http://localhost:45000/DentalApi.svc";
        private ServiceHost _serviceHost;
        private ClientBroadcastService _clientBroadcastService;
        private ServerAppService _serverAppService;
        private DentalApiFactoryService _dentalApiFactoryService;
        private Repository _repository;

        public Service()
        {
            InitializeComponent();
        }

        public void Start(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            _repository = new Repository();
            _clientBroadcastService = new ClientBroadcastService();
            _dentalApiFactoryService = new DentalApiFactoryService(_repository);
            _serverAppService = new ServerAppService(_repository, new ChewsiApi(_clientBroadcastService), _dentalApiFactoryService, _clientBroadcastService);

            _serviceHost = new ServiceHost(_serverAppService);

            ServiceDiscoveryBehavior serviceDiscoveryBehavior = new ServiceDiscoveryBehavior();
            serviceDiscoveryBehavior.AnnouncementEndpoints.Add(new UdpAnnouncementEndpoint());
            _serviceHost.Description.Behaviors.Add(serviceDiscoveryBehavior);
            _serviceHost.AddServiceEndpoint(new UdpDiscoveryEndpoint());

            var binding = new WSDualHttpBinding(WSDualHttpSecurityMode.Message)
            {
                OpenTimeout = new TimeSpan(0, 10, 0),
                CloseTimeout = new TimeSpan(0, 10, 0),
                SendTimeout = new TimeSpan(0, 10, 0),
                ReceiveTimeout = new TimeSpan(0, 10, 0)
            };
            ServiceEndpoint endpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (IServerAppService)), binding,
                new EndpointAddress(Address));
            _serviceHost.AddServiceEndpoint(endpoint);
            _serviceHost.Open();
        }

        protected override void OnStop()
        {
            //_clientBroadcastService.ShowLoadingIndicator("Application server is stopping...");
            Utils.SafeCall(_serviceHost.Close);
            _serverAppService.Dispose();
        }
    }
}