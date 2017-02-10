using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ChewsiPlugin.UI.ViewModels.DialogService
{
    public class Message : ViewModelBase
    {
        private readonly DialogService _dialogService;
        private RelayCommand _closeCommand;

        public Message(DialogService dialogService, string text, string header = null)
        {
            _dialogService = dialogService;
            Text = text;
            Header = header;
        }

        public string Text { get; private set; }

        public string Header { get; private set; }
        
        public ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(OnCloseCommandExecute)); }
        }

        private void OnCloseCommandExecute()
        {
            _dialogService.Close();
        }
    }
}
