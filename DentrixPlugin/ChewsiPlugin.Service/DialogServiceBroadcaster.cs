using System;
using System.Collections.Generic;
using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.Service
{
    public class DialogServiceBroadcaster : IClientCallback, IDialogService
    {
        private readonly Dictionary<string, IClientCallback> _callbackList;

        public DialogServiceBroadcaster()
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

        public void Show(string message, string header = null)
        {
            foreach (var value in _callbackList.Values)
            {
                value.Show(message, header);
            }
        }

        public void Show(string message, string header = null, Action onDialogClosed = null, string buttonText = null)
        {
            foreach (var value in _callbackList.Values)
            {
                value.Show(message, header);
            }
        }

        public void ShowLoadingIndicator()
        {
            foreach (var value in _callbackList.Values)
            {
                value.ShowLoadingIndicator();
            }
        }

        public void Close()
        {
            throw new NotSupportedException();
        }

        public void ShowLoadingIndicator(string message)
        {
            foreach (var value in _callbackList.Values)
            {
                value.ShowLoadingIndicator(message);
            }
        }

        public void HideLoadingIndicator()
        {
            foreach (var value in _callbackList.Values)
            {
                value.HideLoadingIndicator();
            }
        }
    }
}