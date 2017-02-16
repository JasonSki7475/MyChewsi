using System;
using System.Linq;
using System.Windows.Input;
using ChewsiPlugin.Api.Repository;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ChewsiPlugin.UI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly Action<SettingsViewModel> _onClose;
        private Settings.PMS.Types _selectedType;
        private string _path;
        private ICommand _closeCommand;
        private string _address1;
        private string _address2;
        private string _tin;

        public SettingsViewModel(Action<SettingsViewModel> onClose)
        {
            _onClose = onClose;
            Types = new[] {Settings.PMS.Types.Dentrix, Settings.PMS.Types.OpenDental };
            SelectedType = Types.First();
            Path = @"C:\Program Files (x86)\Open Dental";
        }

        public Settings.PMS.Types[] Types { get; private set; }

        public Settings.PMS.Types SelectedType
        {
            get { return _selectedType; }
            set
            {
                _selectedType = value;
                RaisePropertyChanged(() => SelectedType);
                RaisePropertyChanged(() => NeedsPath);
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

        #region CloseCommand
        public ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(OnCloseCommandExecute)); }
        }

        private void OnCloseCommandExecute()
        {
            _onClose(this);
        }
        #endregion   
    }
}
