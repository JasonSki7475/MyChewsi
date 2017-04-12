using System;
using System.Windows.Input;
using ChewsiPlugin.Api.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ChewsiPlugin.UI.ViewModels.DialogService
{
    internal class Message : ViewModelBase, IMessage
    {
        private readonly DialogService _dialogService;
        private readonly Action _dialogResultCallback;
        private RelayCommand _closeCommand;

        public Message(DialogService dialogService, string text, string header = null, Action dialogResultCallback = null)
        {
            _dialogService = dialogService;
            _dialogResultCallback = dialogResultCallback;
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
            RaiseDialogResultCallback();
            _dialogService.Close();
        }

        private void RaiseDialogResultCallback()
        {
            if (_dialogResultCallback != null)
            {
                _dialogResultCallback();
            }
        }
    }
}
