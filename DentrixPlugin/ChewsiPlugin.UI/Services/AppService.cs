using System;
using System.Collections.Generic;
using System.Linq;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using NLog;

namespace ChewsiPlugin.UI.Services
{
    public class AppService : IAppService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IChewsiApi _chewsiApi;
        private readonly IRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly IClaimStoreService _claimStoreService;
        private IDentalApi _dentalApi;
        private Settings.PMS.Types _pmsType;
        private readonly object _dentalApiInitializationLock = new object();

        public AppService(IChewsiApi chewsiApi, IRepository repository, IDialogService dialogService, IClaimStoreService claimStoreService)
        {
            _chewsiApi = chewsiApi;
            _repository = repository;
            _dialogService = dialogService;
            _claimStoreService = claimStoreService;
            _repository.Initialize();
        }

        public bool Initialized => _repository.Initialized && DentalApi != null;

        public void SaveSettings(SettingsDto settingsDto)
        {
            string pmsVersionOld = null;
            Settings.PMS.Types pmsTypeOld = Settings.PMS.Types.Dentrix;
            string address1Old = null;
            string address2Old = null;
            string stateOld = null;
            string osOld = null;
            string pluginVersionOld = null;
            string tinOld = null;

            var firstAppRun = !_repository.Initialized;
            if (!firstAppRun)
            {
                // DB already has all the settings, load current values to detect changes below
                pmsVersionOld = _repository.GetSettingValue<string>(Settings.PMS.VersionKey);
                pmsTypeOld = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
                address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
                stateOld = _repository.GetSettingValue<string>(Settings.StateKey);
                osOld = _repository.GetSettingValue<string>(Settings.OsKey);
                pluginVersionOld = _repository.GetSettingValue<string>(Settings.AppVersionKey);
                tinOld = _repository.GetSettingValue<string>(Settings.TIN);
            }

            _repository.SaveSetting(Settings.TIN, settingsDto.Tin);
            _repository.SaveSetting(Settings.PMS.TypeKey, settingsDto.PmsType);
            _repository.SaveSetting(Settings.PMS.PathKey, settingsDto.PmsPath);
            _repository.SaveSetting(Settings.Address1Key, settingsDto.Address1);
            _repository.SaveSetting(Settings.Address2Key, settingsDto.Address2);
            _repository.SaveSetting(Settings.StateKey, settingsDto.State);
            _repository.SaveSetting(Settings.OsKey, Utils.GetOperatingSystemInfo());
            _repository.SaveSetting(Settings.AppVersionKey, Utils.GetPluginVersion());

            _repository.SaveSetting(Settings.UseProxy, settingsDto.UseProxy);
            _repository.SaveSetting(Settings.ProxyAddress, settingsDto.ProxyAddress);
            _repository.SaveSetting(Settings.ProxyPort, settingsDto.ProxyPort);
            _repository.SaveSetting(Settings.ProxyLogin, settingsDto.ProxyLogin);
            _repository.SaveSetting(Settings.ProxyPassword, settingsDto.ProxyPassword);

            // init DentalApi and get version
            _repository.SaveSetting(Settings.PMS.VersionKey, DentalApi.GetVersion());
            
            // DB is empty, settings saved, call RegisterPlugin
            var registeredBefore = !string.IsNullOrEmpty(_repository.GetSettingValue<string>(Settings.MachineIdKey));
            if (!firstAppRun || !registeredBefore)
            {
                var pmsVersion = DentalApi.GetVersion();
                if (registeredBefore)
                {
                    // detect changes and call UpdatePluginRegistration
                    if ((pmsVersion != pmsVersionOld)
                        || pmsTypeOld != settingsDto.PmsType
                        || address1Old != settingsDto.Address1
                        || address2Old != settingsDto.Address2
                        || stateOld != settingsDto.State
                        || osOld != Utils.GetOperatingSystemInfo()
                        || tinOld != settingsDto.Tin
                        || pluginVersionOld != Utils.GetPluginVersion())
                    {
                        Logger.Info("Configuration changes detected. Updating plugin registration...");
                        var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                        InitializeChewsiApi();
                        _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                        Logger.Info("Plugin registration successfuly updated");
                    }                    
                }
                else
                {
                    Logger.Info("Registering plugin...");
                    var machineId = _chewsiApi.RegisterPlugin(new RegisterPluginRequest(settingsDto.Tin, settingsDto.Address1, settingsDto.Address2, settingsDto.PmsType, pmsVersion));
                    Logger.Info("Machine Id: " + machineId);
                    _repository.SaveSetting(Settings.MachineIdKey, machineId);    
                    InitializeChewsiApi();                                   
                }
            }
        }

        public void InitializeChewsiApi()
        {
            var token = _repository.GetSettingValue<string>(Settings.MachineIdKey);
            var useProxy = _repository.GetSettingValue<bool>(Settings.UseProxy);
            var proxyAddress = _repository.GetSettingValue<string>(Settings.ProxyAddress);
            var proxyPort = _repository.GetSettingValue<int>(Settings.ProxyPort);
            var proxyUserName = _repository.GetSettingValue<string>(Settings.ProxyLogin);
            var proxyPassword = _repository.GetSettingValue<string>(Settings.ProxyPassword);

            _chewsiApi.Initialize(token, useProxy, proxyAddress, proxyPort, proxyUserName, proxyPassword);
        }

        public SettingsDto GetSettings()
        {
            var pmsType = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var pmsPath = _repository.GetSettingValue<string>(Settings.PMS.PathKey);
            var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
            var tin = _repository.GetSettingValue<string>(Settings.TIN);
            var useProxy = _repository.GetSettingValue<bool>(Settings.UseProxy);
            var proxyAddress = _repository.GetSettingValue<string>(Settings.ProxyAddress);
            var proxyPassword = _repository.GetSettingValue<string>(Settings.ProxyPassword);
            var proxyPort = _repository.GetSettingValue<int>(Settings.ProxyPort);
            var proxyLogin = _repository.GetSettingValue<string>(Settings.ProxyLogin);
            var state = _repository.GetSettingValue<string>(Settings.StateKey);
            return new SettingsDto(pmsType, pmsPath, address1Old, address2Old, tin, useProxy, proxyAddress, proxyPort, proxyLogin, proxyPassword, state);
        }

        public void UpdatePluginRegistration()
        {
            if (_dentalApi != null)
            {
                string pmsVersion = _dentalApi.GetVersion();
                var pmsVersionOld = _repository.GetSettingValue<string>(Settings.PMS.VersionKey);
                var pmsTypeOld = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
                var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
                var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
                var osOld = _repository.GetSettingValue<string>(Settings.OsKey);
                var pluginVersionOld = _repository.GetSettingValue<string>(Settings.AppVersionKey);
                if (pmsVersion != pmsVersionOld
                    || osOld != Utils.GetOperatingSystemInfo()
                    || pluginVersionOld != Utils.GetPluginVersion())
                {
                    Logger.Info("Configuration changes detected. Updating plugin registration...");
                    var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                    _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, address1Old,
                        address2Old, pmsTypeOld, pmsVersion));
                    Logger.Info("Plugin registration successfuly updated");
                }
            }
        }

        public ValidateSubscriberAndProviderResponse ValidateClaim(string providerId, string patientId, 
            out ProviderInformation providerInformation, out SubscriberInformation subscriberInformation, out Provider provider)
        {
            try
            {
                _dialogService.ShowLoadingIndicator();
                provider = DentalApi.GetProvider(providerId);
                if (provider != null)
                {
                    var subscriberInfo = DentalApi.GetPatientInfo(patientId);
                    if (subscriberInfo != null)
                    {
                        providerInformation = new ProviderInformation
                        {
                            NPI = provider.Npi,
                            TIN = provider.Tin
                        };
                        subscriberInformation = new SubscriberInformation
                        {
                            Id = subscriberInfo.PrimaryInsuredId,
                            SubscriberDateOfBirth = subscriberInfo.BirthDate,
                            SubscriberFirstName = subscriberInfo.FirstName,
                            SubscriberLastName = subscriberInfo.LastName
                        };
                        var providerAddress = new ProviderAddressInformation
                        {
                            RenderingAddress1 = provider.AddressLine1,
                            RenderingAddress2 = provider.AddressLine2,
                            RenderingCity = provider.City,
                            RenderingState = provider.State,
                            RenderingZip = provider.ZipCode,
                        };
                        try
                        {
                            var validationResponse = _chewsiApi.ValidateSubscriberAndProvider(providerInformation, providerAddress, subscriberInformation);
                            Logger.Debug($"Validated subscriber '{patientId}' and provider '{providerId}': '{validationResponse.ValidationPassed}'");

                            return validationResponse;
                        }
                        catch (NullReferenceException)
                        {
                            _dialogService.Show("Invalid server response. Try again later", "Error");
                        }
                    }
                    else
                    {
                        var msg = "Cannot find patient " + patientId;
                        _dialogService.Show(msg, "Error");
                        Logger.Error(msg);
                    }
                }
                else
                {
                    var msg = "Cannot find provider " + providerId;
                    _dialogService.Show(msg, "Error");
                    Logger.Error(msg);
                }
                providerInformation = null;
                subscriberInformation = null;
                return null;
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public void SubmitClaim(string patientId, ProviderInformation providerInformation, SubscriberInformation subscriberInformation, Provider provider)
        {
            try
            {
                _dialogService.ShowLoadingIndicator();
                var procedures = DentalApi.GetProcedures(patientId);
                if (procedures.Any())
                {
                    _chewsiApi.ProcessClaim(providerInformation, subscriberInformation, procedures.Select(m => new ClaimLine(m.Date, m.Code, m.Amount)).ToList());
                    _claimStoreService.RequestStatusLookup(provider);

                    Logger.Debug($"Processed claim, found '{procedures.Count}' procedures.");
                }
                else
                {
                    var msg = "Cannot find any procedures for the patient";
                    _dialogService.Show(msg, "Error");
                    Logger.Error(msg + " " + patientId);
                }
            }
            finally
            {
                _dialogService.HideLoadingIndicator();
            }
        }

        public void UpdateCachedClaim(string chewsiId, DateTime date, AppointmentState state, string statusText)
        {
            var a = _repository.GetAppointmentByChewsiIdAndDate(chewsiId, date);
            if (a != null)
            {
                a.StatusText = statusText;
                a.State = state;

                _repository.UpdateAppointment(a);
            }
        }

        private IDentalApi DentalApi
        {
            get
            {
                var pmsTypeString = _repository.GetSettingValue<string>(Settings.PMS.TypeKey);
                if (pmsTypeString != null)
                {
                    var pmsType = (Settings.PMS.Types)Enum.Parse(typeof(Settings.PMS.Types), pmsTypeString);
                    lock (_dentalApiInitializationLock)
                    {
                        if (_dentalApi == null || pmsType != _pmsType)
                        {
                            // free resources when user changes PMS type
                            if (_dentalApi != null && pmsType != _pmsType)
                            {
                                _dentalApi.Unload();
                            }
                            // create new instance of API on app start and settings changes
                            Logger.Debug("Initializing {0} API", pmsTypeString);
                            switch (pmsType)
                            {
                                case Settings.PMS.Types.Dentrix:
                                    _dentalApi = new DentrixApi(_dialogService);
                                    break;
                                case Settings.PMS.Types.OpenDental:
                                    _dentalApi = new OpenDentalApi.OpenDentalApi(_repository, _dialogService);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            _pmsType = pmsType;
                        }
                        return _dentalApi;                        
                    }
                }
                Logger.Error("PMS system type is not set");
                return null;
            }
        }


    }
}
