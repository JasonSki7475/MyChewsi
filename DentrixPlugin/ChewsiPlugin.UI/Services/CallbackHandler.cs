using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using NLog;

namespace ChewsiPlugin.UI.Services
{
    internal class CallbackHandler : IClientCallback
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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
            Logger.Debug("Lock claim {0}", id);
            _clientAppService.LockClaim(id);
        }

        public void UnlockClaim(string id)
        {
            Logger.Debug("Unlock claim {0}", id);
            _clientAppService.UnlockClaim(id);
        }

        public void SetClaims(List<ClaimDto> claims)
        {
            Logger.Debug("Broadcasted {0} updated claims", claims.Count);
            _clientAppService.SetClaims(claims);
        }
    }
}
