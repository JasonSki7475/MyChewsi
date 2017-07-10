using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Discovery;
using System.ServiceProcess;
using System.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.Service.Services;
using NLog;

namespace ChewsiPlugin.Service
{
    public partial class Service : ServiceBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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

        private void ApplicationDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Error(exception,
                e.IsTerminating
                    ? "Application domain unhandled exception has been thrown, application will be terminated"
                    : "Application domain unhandled exception has been thrown");
        }

        protected override void OnStart(string[] args)
        {
            //Thread.Sleep(20000);
            AppDomain.CurrentDomain.UnhandledException += ApplicationDomainUnhandledException;

            _repository = new Repository();
            _clientBroadcastService = new ClientBroadcastService();
            _dentalApiFactoryService = new DentalApiFactoryService(_repository);
            _serverAppService = new ServerAppService(_repository, new ChewsiApi(), _dentalApiFactoryService, _clientBroadcastService);

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
                ReceiveTimeout = new TimeSpan(0, 10, 0),
                Security = new WSDualHttpSecurity
                {
                    Mode = WSDualHttpSecurityMode.None
                }
            };
            ServiceEndpoint endpoint = new ServiceEndpoint(ContractDescription.GetContract(typeof (IServerAppService)), binding,
                new EndpointAddress(Utils.GetAddressFromHost(System.Net.Dns.GetHostName())));
            _serviceHost.AddServiceEndpoint(endpoint);
            _serviceHost.Open();
        }

        protected override void OnStop()
        {
            //_clientBroadcastService.ShowLoadingIndicator("Application server is stopping...");
            Utils.SafeCall(() => _serviceHost.Close());
            _serverAppService.Dispose();
        }
    }
}