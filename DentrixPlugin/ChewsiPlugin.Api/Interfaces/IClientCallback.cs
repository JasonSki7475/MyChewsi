using System;
using System.Collections.Generic;
using System.ServiceModel;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    [ServiceContract]
    public interface IClientCallback
    {
        [OperationContract]
        void Show(string message, string header = null, string buttonText = null);

        [OperationContract(Name = "ShowIndicator")]
        void ShowLoadingIndicator();

        [OperationContract]
        void ShowLoadingIndicator(string message);

        [OperationContract]
        void HideLoadingIndicator();

        [OperationContract]
        void LockClaim(string id);

        [OperationContract]
        void UnlockClaim(string id);

        [OperationContract]
        void SetClaims(List<ClaimDto> claims);
    }
}
