using System;
using System.Windows.Forms;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.Api.Common
{
    public class MessageBoxDialogService : IDialogService
    {
        public IMessage Message { get; }
        public bool IsLoading { get; }
        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Show(string message, string header = null, Action onDialogClosed = null)
        {
            MessageBox.Show(message, header);
        }

        public void ShowLoadingIndicator()
        {
            throw new NotImplementedException();
        }

        public void HideLoadingIndicator()
        {
            throw new NotImplementedException();
        }
    }
}
