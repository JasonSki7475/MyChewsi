using System;

namespace ChewsiPlugin.UI.ViewModels.DialogService
{
    public interface IDialogService
    {
        Message Message { get; }
        void Close();
        void Show(string message, string header = null, Action onDialogClosed = null);
    }
}