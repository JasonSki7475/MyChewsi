using System;
using System.Collections.Generic;
using System.ServiceModel;
using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.Api.Interfaces
{
    [ServiceContract(CallbackContract = typeof(IClientCallback))]
    public interface IServerAppService : IDisposable
    {
        [OperationContract]
        SettingsDto GetSettings();

        [OperationContract]
        void SaveSettings(SettingsDto settingsDto);

        [OperationContract]
        void DeleteAppointment(string id);

        [OperationContract]
        void ValidateAndSubmitClaim(string id);

        [OperationContract]
        List<ClaimDto> GetClaims(bool force);
        
        [OperationContract]
        ServerState InitClient();

        [OperationContract]
        void DisconnectClient();

        [OperationContract]
        Provider GetProvider(string providerId);

        [OperationContract]
        InitialSettingsDto GetInitialSettings();

        /// <summary>
        /// For tests, InitClient() should be used
        /// </summary>
        ServerState GetState();
    }
}
