using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.Threading;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Interfaces;
using NLog;

namespace ChewsiPlugin.Api
{
    public class ServiceDiscovery
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly DiscoveryClient _discoveryClient;
        private bool _isRunning;
        private EndpointAddress _address;

        public ServiceDiscovery()
        {
            _discoveryClient = new DiscoveryClient(new UdpDiscoveryEndpoint());
            _discoveryClient.FindProgressChanged += DiscoveryClientOnFindProgressChanged;
            _discoveryClient.FindCompleted += DiscoveryClientOnFindCompleted;
        }

        public async Task<EndpointAddress> Discover()
        {
            if (_isRunning)
            {
                _discoveryClient.CancelAsync(null);
            }
            return await Task<EndpointAddress>.Factory.StartNew(() =>
            {
                _isRunning = true;
                _discoveryClient.FindAsync(new FindCriteria(typeof(IServerAppService)));
                while (true)
                {
                    Thread.Sleep(100);
                    if (!_isRunning)
                    {
                        Logger.Debug("Service discovery returns endpoint {0}", _address);
                        return _address;
                    }
                }
            }).ConfigureAwait(false);
        }

        private void DiscoveryClientOnFindCompleted(object sender, FindCompletedEventArgs findCompletedEventArgs)
        {
            Logger.Debug("Service discovery completed. Found {0} endpoints", findCompletedEventArgs.Result.Endpoints.Count);
            _isRunning = false;
        }

        private void DiscoveryClientOnFindProgressChanged(object sender, FindProgressChangedEventArgs findProgressChangedEventArgs)
        {
            Logger.Debug("Service discovery progress changed. Found service @ {0}", findProgressChangedEventArgs.EndpointDiscoveryMetadata.Address);
            _address = findProgressChangedEventArgs.EndpointDiscoveryMetadata.Address;
            _isRunning = false;
        }
    }
}