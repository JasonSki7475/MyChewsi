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
        void Initialize(bool firstRun);

        [OperationContract]
        void DeleteAppointment(string id);

        [OperationContract]
        void ValidateAndSubmitClaim(string appointmentId, DateTime date, string providerId, string patientId, DateTime pmsModifiedDate);

        bool Initialized { get; }

        [OperationContract]
        void RefreshAppointments(bool loadFromPms, bool loadFromService);

        [OperationContract]
        List<ClaimDto> LoadAppointments(bool loadFromPms, bool loadFromService);

        [OperationContract]
        bool InitClient();

        [OperationContract]
        void DisconnectClient();
    }
}
