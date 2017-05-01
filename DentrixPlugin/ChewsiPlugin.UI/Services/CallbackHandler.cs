using System;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.UI.Services
{
    internal class CallbackHandler : IClientCallback
    {
        private readonly IDialogService _dialogService;

        public CallbackHandler(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public void Show(string message, string header = null)
        {
            _dialogService.Show(message, header);
        }

        public void ShowLoadingIndicator()
        {
            _dialogService.ShowLoadingIndicator();
        }

        public void ShowLoadingIndicator(string message)
        {
            _dialogService.ShowLoadingIndicator(message);
        }

        public void HideLoadingIndicator()
        {
            _dialogService.HideLoadingIndicator();
        }
    }
}
