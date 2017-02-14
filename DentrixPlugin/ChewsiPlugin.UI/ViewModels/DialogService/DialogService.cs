using System.Collections.Generic;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels.DialogService
{
    public class DialogService : ViewModelBase, IDialogService
    {
        private readonly Queue<Message> _lightBoxes = new Queue<Message>();

        public Message Message
        {
            get { return _lightBoxes.Count != 0 ? _lightBoxes.Peek() : null; }
            private set
            {
                _lightBoxes.Enqueue(value);
                RaisePropertyChanged(() => Message);
            }
        }

        public void Show(string message, string header = null)
        {
            Message = new Message(this, message, header);
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
