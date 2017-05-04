using System;
using System.Windows.Forms;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.Api.Common
{
    public class MessageBoxDialogService : IDialogService
    {
        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Show(string message, string header = null, string buttonText = null)
        {
            MessageBox.Show(message, header);
        }

        public void ShowLoadingIndicator()
        {
            throw new NotImplementedException();
        }
        public void ShowLoadingIndicator(string message)
        {
            throw new NotImplementedException();
        }

        public void HideLoadingIndicator()
        {
            throw new NotImplementedException();
        }
    }
}
