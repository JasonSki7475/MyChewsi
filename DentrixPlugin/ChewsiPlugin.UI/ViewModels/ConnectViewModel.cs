using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class ConnectViewModel : ViewModelBase, IConnectViewModel
    {
        private IClientAppService _clientAppService;
        private string _host;
        private ICommand _detectCommand;
        private ICommand _connectCommand;
        private bool _shown;

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

        private void OnDetectCommandExecute()
        {
            Task.Factory.StartNew(() =>
            {
                var address = _clientAppService.FindServerAndInitChannelAsync();
                if (address != null)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        Host = Utils.GetHostFromAddress(address);
                    });
                }
            });
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