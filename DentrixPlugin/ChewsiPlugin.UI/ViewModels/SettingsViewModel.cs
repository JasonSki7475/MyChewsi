using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class SettingsViewModel : ViewModelBase, ISettingsViewModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Action _onClose;
        private readonly IDialogService _dialogService;
        private Settings.PMS.Types _selectedType;
        private string _path;
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
        private ICommand _selectPathCommand;
        private string _state;
        private bool _startPms;
        private bool _startLauncher;
        private bool _isVisible;
        private IClientAppService _appService;
        private string _machineId;
        private string _closeButtonText;

        public SettingsViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            Types = new[] {Settings.PMS.Types.Dentrix, Settings.PMS.Types.Eaglesoft, Settings.PMS.Types.OpenDental };
        }

        public void Fill(string addressLine1, string addressLine2, string state, string tin, bool startLauncher, string proxyAddress, int proxyPort)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Address1 = addressLine1;
                Address2 = addressLine2;
                State = state;
                Tin = tin;
                StartLauncher = startLauncher;

                ProxyAddress = proxyAddress;
                ProxyPort = proxyPort;
            });
        }

        public string CloseButtonText
        {
            get { return _closeButtonText; }
            set
            {
                _closeButtonText = value;
                RaisePropertyChanged(() => CloseButtonText);
            }
        }

        public bool IsServer { get; set; }

        public Settings.PMS.Types[] Types { get; private set; }
        
        public void Show(Action onClose)
        {
            _onClose = onClose;
            IsVisible = true;
        }

        public void InjectAppServiceAndInit(IClientAppService appService, SettingsDto settings)
        {
            _appService = appService;
            
            _address1 = settings.Address1;
            _address2 = settings.Address2;
            _tin = settings.Tin;
            _useProxy = settings.UseProxy;
            _proxyAddress = settings.ProxyAddress;
            _proxyPort = settings.ProxyPort;
            _proxyLogin = settings.ProxyLogin;
            _proxyPassword = settings.ProxyPassword;
            _selectedType = settings.PmsType;
            _path = settings.PmsPath;
            _state = settings.State;
            _startPms = settings.StartPms;
            _startLauncher = settings.StartLauncher;
            _machineId = settings.MachineId;
            IsServer = !settings.IsClient;

            CloseButtonText = IsServer ? "Save" : "Close";
        }

        private void Hide()
        {
            IsVisible = false;
            _onClose?.Invoke();
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
                RaisePropertyChanged(() => NeedsPath);
                RaisePropertyChanged(() => CanChangeStartPms);
            }
        }

        public string Path
        {
            get { return _path; }
            set
            {
                _path = value;
                RaisePropertyChanged(() => Path);
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

        public bool NeedsPath
        {
            get { return SelectedType == Settings.PMS.Types.OpenDental; }
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
        
        public bool CanChangeStartPms => SelectedType != Settings.PMS.Types.Eaglesoft;

        #region CloseCommand
        public ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(OnCloseCommandExecute, CanCloseCommandExecute)); }
        }

        private bool CanCloseCommandExecute()
        {
            return false;
        }

        private void OnCloseCommandExecute()
        {
            Hide();
        }
        #endregion 
          
        #region SaveCommand
        public ICommand SaveCommand
        {
            get { return _saveCommand ?? (_saveCommand = new RelayCommand(OnSaveCommandExecute)); }
        }
        
        private void OnSaveCommandExecute()
        {
            if (IsServer)
            {
                try
                {
                    if (SelectedType == Settings.PMS.Types.OpenDental)
                    {
                        if (string.IsNullOrEmpty(Path) || !Directory.Exists(Path))
                        {
                            _dialogService.Show("Path to OpenDental directory should be set", "Error");
                            return;
                        }
                    }

                    _dialogService.ShowLoadingIndicator();
                    _appService.SaveSettings(new SettingsDto(SelectedType, Path, Address1, Address2, Tin, UseProxy,
                        ProxyAddress, ProxyPort, ProxyLogin, ProxyPassword,
                        State, StartPms, StartLauncher, _machineId, !IsServer));
                }
                finally
                {
                    _dialogService.HideLoadingIndicator();
                }
                Logger.Debug("Settings were saved");
            }
            Hide();
        }
        #endregion   

        #region SelectPathCommand
        public ICommand SelectPathCommand
        {
            get { return _selectPathCommand ?? (_selectPathCommand = new RelayCommand(OnSelectPathCommandExecute, CanSelectPathCommandExecute)); }
        }

        private bool CanSelectPathCommandExecute()
        {
            return NeedsPath;
        }

        private void OnSelectPathCommandExecute()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    Path = dialog.SelectedPath;
                }
            }
        }
        #endregion 
    }
}
