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

        public Message(DialogService dialogService, string text, string header = null, Action dialogResultCallback = null, string buttonText = "Ok")
        {
            _dialogService = dialogService;
            _dialogResultCallback = dialogResultCallback;
            Text = text;
            ButtonText = buttonText;
            Header = header;
        }

        public string Text { get; }

        public string ButtonText { get; }

        public string Header { get; }
        
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
            _dialogResultCallback?.Invoke();
        }
    }
}
