using ChewsiPlugin.Api.Interfaces;

namespace ChewsiPlugin.Service
{
    internal interface IClientCallbackService: IClientCallback
    {
        void AddClient(string sessionId, IClientCallback callback);
        void RemoveClient(string sessionId);
    }
}
