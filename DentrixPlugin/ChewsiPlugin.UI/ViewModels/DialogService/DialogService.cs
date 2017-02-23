using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Interfaces;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels.DialogService
{
    public class DialogService : ViewModelBase, IDialogService
    {
        private readonly Queue<IMessage> _lightBoxes = new Queue<IMessage>();
        private bool _isLoading;
        
        public IMessage Message
        {
            get { return _lightBoxes.Count != 0 ? _lightBoxes.Peek() : null; }
            private set
            {
                _lightBoxes.Enqueue(value);
                RaisePropertyChanged(() => Message);
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                RaisePropertyChanged(() => IsLoading);
            }
        }

        public void Show(string message, string header = null, Action onDialogClosed = null)
        {
            Message = new Message(this, message, header, onDialogClosed);
        }

        public void ShowLoadingIndicator()
        {
            IsLoading = true;
        }

        public void HideLoadingIndicator()
        {
            IsLoading = false;
        }

        public void Close()
        {
            if (_lightBoxes.Count != 0)
            {
                _lightBoxes.Dequeue();
                RaisePropertyChanged("Message");
            }
        }
    }
}
