using System.Collections.Generic;
using System.Threading.Tasks;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using NLog;

namespace ChewsiPlugin.Service.Services
{
    internal class ClientBroadcastService : IClientCallbackService, IDialogService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, IClientCallback> _callbackList;

        public ClientBroadcastService()
        {
            _callbackList = new Dictionary<string, IClientCallback>();
        }

        public void AddClient(string sessionId, IClientCallback callback)
        {
            if (!_callbackList.ContainsKey(sessionId))
            {
                _callbackList.Add(sessionId, callback);
            }
        }

        public void RemoveClient(string sessionId)
        {
            if (_callbackList.ContainsKey(sessionId))
            {
                _callbackList.Remove(sessionId);
            }
        }

        public void Show(string message, string header = null, string buttonText = null)
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Utils.SafeCall(callback.Show, message, header, buttonText);
            });
        }

        public void ShowLoadingIndicator()
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Utils.SafeCall(callback.ShowLoadingIndicator);
            });
        }

        public void ShowLoadingIndicator(string message)
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Utils.SafeCall(callback.ShowLoadingIndicator, message);
            });
        }

        public void HideLoadingIndicator()
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Utils.SafeCall(callback.HideLoadingIndicator);
            });
        }

        public void LockClaim(string id)
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Logger.Debug("Lock claim {0}", id);
                Utils.SafeCall(callback.LockClaim, id);
            });
        }

        public void UnlockClaim(string id)
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Logger.Debug("Unlock claim {0}", id);
                Utils.SafeCall(callback.UnlockClaim, id);
            });
        }

        public void SetClaims(List<ClaimDto> claims)
        {
            Parallel.ForEach(_callbackList.Values, callback =>
            {
                Logger.Debug("Broadcasting {0} updated claims", claims.Count);
                Utils.SafeCall(callback.SetClaims, claims);
            });
        }
    }
}