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
        void ShowLoadingIndicator(string message);

        [OperationContract]
        void LockClaim(string id);

        [OperationContract]
        void UnlockClaim(string id);

        [OperationContract]
        void SetClaims(List<ClaimDto> claims);
    }
}
