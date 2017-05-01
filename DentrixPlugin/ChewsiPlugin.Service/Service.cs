using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Discovery;
using System.ServiceProcess;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Service
{
    public partial class Service : ServiceBase
    {
        private const string Address = "http://localhost:45000/DentalApi.svc";
        private ServiceHost _serviceHost;
        private DialogServiceBroadcaster _dialogServiceBroadcaster;
        private ServerAppService _serverAppService;
        private DentalApiFactoryService _dentalApiFactoryService;
        private Repository _repository;

        public Service()
        {
            InitializeComponent();
        }

        /*
                       <behavior name="AnnouncementBehavior">
                <!--Add Discovery behavior-->
                <serviceDiscovery>
                  <announcementEndpoints>
                    <endpoint kind="udpAnnouncementEndpoint" />
                  </announcementEndpoints>
                </serviceDiscovery>
              </behavior>
             */

        /*

                   <!--Add Discovery Endpoint-->
      <endpoint name="udpDiscoveryEpt" kind="udpDiscoveryEndpoint" />
         */

        public void Start(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            _repository = new Repository();
            _dialogServiceBroadcaster = new DialogServiceBroadcaster();
            _dentalApiFactoryService = new DentalApiFactoryService(_dialogServiceBroadcaster, _repository);
            _serverAppService = new ServerAppService(_repository, new ChewsiApi(_dialogServiceBroadcaster), _dentalApiFactoryService, _dialogServiceBroadcaster);

            _serviceHost = new ServiceHost(_serverAppService);

            ServiceDiscoveryBehavior serviceDiscoveryBehavior = new ServiceDiscoveryBehavior();
            serviceDiscoveryBehavior.AnnouncementEndpoints.Add(new UdpAnnouncementEndpoint());
            _serviceHost.Description.Behaviors.Add(serviceDiscoveryBehavior);
            _serviceHost.AddServiceEndpoint(new UdpDiscoveryEndpoint());
            
            var binding = new WSDualHttpBinding(WSDualHttpSecurityMode.Message);
            ServiceEndpoint endpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (IServerAppService)), binding,
                new EndpointAddress(Address));
            _serviceHost.AddServiceEndpoint(endpoint);

#if DEBUG
            _serviceHost.Description.Behaviors.Add(new ServiceMetadataBehavior
            {
                HttpGetEnabled = true,
                HttpGetUrl = new Uri("http://localhost:45000/mex")
            });
            //_serviceHost.Description.Behaviors.Add(new ServiceDebugBehavior {IncludeExceptionDetailInFaults = true});

            /*Binding mexBinding = MetadataExchangeBindings.CreateMexHttpBinding();
            ContractDescription contractDescription = ContractDescription.GetContract(typeof(IMetadataExchange));
            contractDescription.Behaviors.Add(new ServiceMetadataContractBehavior(true));
            ServiceEndpoint mexEndpoint = new ServiceEndpoint(contractDescription, mexBinding,
                new EndpointAddress("http://localhost:45000/mex"))
            {
                Name = "mex"
            };
            _serviceHost.AddServiceEndpoint(mexEndpoint);*/
            //_serviceHost.BaseAddresses
#endif

            _serviceHost.Open();
        }

        protected override void OnStop()
        {
            _dialogServiceBroadcaster.ShowLoadingIndicator("Application server is stopping...");
            _serviceHost.Close();
            _serverAppService.Dispose();
        }
    }
}
