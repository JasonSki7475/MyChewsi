using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class SettingsViewModel : ViewModelBase, ISettingsViewModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Action _onClose;
        private readonly IDialogService _dialogService;
        private Settings.PMS.Types _selectedType;
        private ICommand _closeCommand;
        private string _address1;
        private string _address2;
        private string _tin;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyLogin;
        private string _proxyPassword;
        private ICommand _saveCommand;
        private string _state;
        private bool _startPms;
        private bool _startLauncher;
        private bool _isVisible;
        private IClientAppService _appService;
        private string _machineId;
        private bool _isClient;
        private string _serverHost;
        private string _city;
        private string _zip;

        public SettingsViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }
        
        public bool IsClient
        {
            get { return _isClient; }
            set
            {
                _isClient = value;
                RaisePropertyChanged(() => IsClient);
            }
        }
        
        public void Show(Action onClose)
        {
            _onClose = onClose;
            IsVisible = true;
        }

        public void InjectAppServiceAndInit(IClientAppService appService, SettingsDto settings, string serverAddress, bool startLauncher, bool isClient)
        {
            _appService = appService;
            
            Address1 = settings.Address1;
            Address2 = settings.Address2;
            Tin = settings.Tin;
            UseProxy = settings.UseProxy;
            ProxyAddress = settings.ProxyAddress;
            ProxyPort = settings.ProxyPort;
            ProxyPassword = settings.ProxyPassword;
            SelectedType = settings.PmsType;
            State = settings.State;
            StartPms = settings.StartPms;
            StartLauncher = startLauncher;
            _machineId = settings.MachineId;
            IsClient = isClient;
            City = settings.City;
            Zip = settings.Zip;
            ServerHost = Utils.GetHostFromAddress(serverAddress);
        }

        private void Hide()
        {
            IsVisible = false;
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            private set
            {
                _isVisible = value;
                RaisePropertyChanged(() => IsVisible);
            }
        }

        public Settings.PMS.Types SelectedType
        {
            get { return _selectedType; }
            set
            {
                _selectedType = value;
                RaisePropertyChanged(() => SelectedType);
            }
        }

        public string ServerHost
        {
            get { return _serverHost; }
            set
            {
                _serverHost = value;
                RaisePropertyChanged(() => ServerHost);
            }
        }
        
        public string Address1
        {
            get { return _address1; }
            set
            {
                _address1 = value;
                RaisePropertyChanged(() => Address1);
            }
        }

        public string Address2
        {
            get { return _address2; }
            set
            {
                _address2 = value;
                RaisePropertyChanged(() => Address2);
            }
        }

        public string City
        {
            get { return _city; }
            set
            {
                _city = value;
                RaisePropertyChanged(() => City);
            }
        }

        public string Zip
        {
            get { return _zip; }
            set
            {
                _zip = value;
                RaisePropertyChanged(() => Zip);
            }
        }

        public string State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged(() => State);
            }
        }

        public string Tin
        {
            get { return _tin; }
            set
            {
                _tin = value;
                RaisePropertyChanged(() => Tin);
            }
        }

        public bool UseProxy
        {
            get { return _useProxy; }
            set
            {
                _useProxy = value;
                RaisePropertyChanged(() => UseProxy);
            }
        }
        
        public bool StartPms
        {
            get { return _startPms; }
            set
            {
                _startPms = value;
                RaisePropertyChanged(() => StartPms);
            }
        } 
               
        public bool StartLauncher
        {
            get { return _startLauncher; }
            set
            {
                _startLauncher = value;
                RaisePropertyChanged(() => StartLauncher);
            }
        }

        public string ProxyAddress
        {
            get { return _proxyAddress; }
            set
            {
                _proxyAddress = value;
                RaisePropertyChanged(() => ProxyAddress);
            }
        }

        public int ProxyPort
        {
            get { return _proxyPort; }
            set
            {
                _proxyPort = value;
                RaisePropertyChanged(() => ProxyPort);
            }
        }

        public string ProxyLogin
        {
            get { return _proxyLogin; }
            set
            {
                _proxyLogin = value;
                RaisePropertyChanged(() => ProxyLogin);
            }
        }

        public string ProxyPassword
        {
            get { return _proxyPassword; }
            set
            {
                _proxyPassword = value;
                RaisePropertyChanged(() => ProxyPassword);
            }
        }
        
        #region CloseCommand
        public ICommand CloseCommand => _closeCommand ?? (_closeCommand = new RelayCommand(OnCloseCommandExecute, CanCloseCommandExecute));

        private bool CanCloseCommandExecute()
        {
            return false;
        }

        private void OnCloseCommandExecute()
        {
            Hide();
            _onClose?.Invoke();
        }
        #endregion 
          
        #region SaveCommand
        public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(OnSaveCommandExecute));

        private void OnSaveCommandExecute()
        {
            Hide();
            try
            {
                _dialogService.ShowLoadingIndicator();
                _appService.SaveSettings(new SettingsDto(SelectedType, Address1, Address2, Tin, UseProxy,
                    ProxyAddress, ProxyPort, ProxyLogin, ProxyPassword,
                    State, StartPms, _machineId, City, Zip), Utils.GetAddressFromHost(ServerHost), StartLauncher);
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
            Logger.Debug("Settings were saved");
            _onClose?.Invoke();
        }
        #endregion   
    }
}
