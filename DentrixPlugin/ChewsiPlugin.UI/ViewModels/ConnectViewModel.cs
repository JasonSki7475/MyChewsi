using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class ConnectViewModel : ViewModelBase, IConnectViewModel
    {
        private readonly IDialogService _dialogService;
        private IClientAppService _clientAppService;
        private string _host;
        private ICommand _detectCommand;
        private ICommand _connectCommand;
        private bool _shown;
        private readonly ServiceDiscovery _serviceDiscovery;

        public ConnectViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            _serviceDiscovery = new ServiceDiscovery();
        }

        public void InjectAppServiceAndInit(IClientAppService appService)
        {
            _clientAppService = appService;
        }

        public void Show(string address)
        {
            Shown = true;
            Host = Utils.GetHostFromAddress(address);
        }

        public bool Shown
        {
            get { return _shown; }
            private set
            {
                _shown = value;
                RaisePropertyChanged(() => Shown);
            }
        }

        public string Host
        {
            get { return _host; }
            set
            {
                _host = value;
                RaisePropertyChanged(() => Host);
            }
        }

        #region DetectCommand
        public ICommand DetectCommand => _detectCommand ?? (_detectCommand = new RelayCommand(OnDetectCommandExecute));

        private async void OnDetectCommandExecute()
        {
            _dialogService.ShowLoadingIndicator("Searching for the Chewsi Server in local network...");
            var address = await _serviceDiscovery.Discover();
            if (address != null)
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    Host = Utils.GetHostFromAddress(address.Uri.ToString());
                });
            }
            else
            {
                _dialogService.Show("Server not found", "Completed", "Ok");
            }
            _dialogService.HideLoadingIndicator();
        }

        #endregion 

        #region ConnectCommand
        public ICommand ConnectCommand => _connectCommand ?? (_connectCommand = new RelayCommand(OnConnectCommandExecute));

        private void OnConnectCommandExecute()
        {
            Task.Factory.StartNew(() => _clientAppService.Connect(Utils.GetAddressFromHost(Host)));
            Shown = false;
        }
        #endregion
    }
}