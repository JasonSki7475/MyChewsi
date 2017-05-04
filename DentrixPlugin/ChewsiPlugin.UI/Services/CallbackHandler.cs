using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.UI.Services
{
    internal class CallbackHandler : IClientCallback
    {
        private readonly IDialogService _dialogService;
        private readonly IClientAppService _clientAppService;

        public CallbackHandler(IDialogService dialogService, IClientAppService clientAppService)
        {
            _dialogService = dialogService;
            _clientAppService = clientAppService;
        }

        public void Show(string message, string header = null, string buttonText = null)
        {
            _dialogService.Show(message, header, buttonText);
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

        public void LockClaim(string id)
        {
            _clientAppService.LockClaim(id);
        }

        public void UnlockClaim(string id)
        {
            _clientAppService.UnlockClaim(id);
        }

        public void SetClaims(List<ClaimDto> claims)
        {
            _clientAppService.SetClaims(claims);
        }
    }
}
