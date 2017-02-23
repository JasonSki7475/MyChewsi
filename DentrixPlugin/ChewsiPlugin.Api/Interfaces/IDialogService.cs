using System;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDialogService
    {
        IMessage Message { get; }
        bool IsLoading { get; }
        void Close();
        void Show(string message, string header = null, Action onDialogClosed = null);
        void ShowLoadingIndicator();
        void HideLoadingIndicator();
    }
}