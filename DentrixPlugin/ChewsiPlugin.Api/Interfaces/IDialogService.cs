using System;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDialogService
    {
        //IMessage Message { get; }
        //string LoadingMessage { get; }
        //bool IsLoading { get; }
        void Show(string message, string header = null, Action onDialogClosed = null, string buttonText = null);
        void ShowLoadingIndicator();
        void Close();
        void ShowLoadingIndicator(string message);
        void HideLoadingIndicator();
    }
}