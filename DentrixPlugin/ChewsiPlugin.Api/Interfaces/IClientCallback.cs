using System;
using System.ServiceModel;

namespace ChewsiPlugin.Api.Interfaces
{
    [ServiceContract]
    public interface IClientCallback
    {
        [OperationContract]
        void Show(string message, string header = null);

        [OperationContract(Name = "ShowIndicator")]
        void ShowLoadingIndicator();

        [OperationContract]
        void ShowLoadingIndicator(string message);

        [OperationContract]
        void HideLoadingIndicator();
    }
}
