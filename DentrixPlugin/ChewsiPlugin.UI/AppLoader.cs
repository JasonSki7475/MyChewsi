using System;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using NLog;
using Settings = ChewsiPlugin.Api.Repository.Settings;

namespace ChewsiPlugin.UI
{
    public class AppLoader : IAppLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IChewsiApi _chewsiApi;
        private readonly IRepository _repository;
        private readonly ISettings _settings;

        public AppLoader(ISettings settings, IChewsiApi chewsiApi, IRepository repository)
        {
            _settings = settings;
            _chewsiApi = chewsiApi;
            _repository = repository;
        }

        public bool IsInitialized()
        {
            return _settings.Initialized() && _repository.Initialized();
        }

        public void RegisterPlugin(Settings.PMS.Types pmsType, string address1, string address2, string tin, string pmsVersion)
        {
            _repository.SaveSetting(Settings.PMS.VersionKey, pmsVersion);
            if (string.IsNullOrEmpty(_repository.GetSettingValue<string>(Settings.MachineIdKey)))
            {
                Logger.Info("Registering plugin...");
                var machineId = _chewsiApi.RegisterPlugin(new RegisterPluginRequest(tin, address1, address2, pmsType, pmsVersion));
                Logger.Info("Machine Id: " + machineId);
                _repository.SaveSetting(Settings.MachineIdKey, machineId);
            }
        }

        public void Initialize(Settings.PMS.Types pmsType, string pmsPath, string address1, string address2, string tin)
        {
            _settings.Initialize();
            _repository.Initialize();

            _repository.SaveSetting(Settings.TIN, tin);
            _repository.SaveSetting(Settings.PMS.TypeKey, pmsType);
            _repository.SaveSetting(Settings.PMS.PathKey, pmsPath);
            _repository.SaveSetting(Settings.Address1Key, address1);
            _repository.SaveSetting(Settings.Address2Key, address2);
            _repository.SaveSetting(Settings.OsKey, Utils.GetOperatingSystemInfo());
            _repository.SaveSetting(Settings.AppVersionKey, Utils.GetPluginVersion());
        }

        public void UpdatePluginRegistration(Settings.PMS.Types pmsType, string address1, string address2, string tin, string pmsVersion)
        {
            var pmsVersionOld = _repository.GetSettingValue<string>(Settings.PMS.VersionKey);
            var pmsTypeOld = _repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            var address1Old = _repository.GetSettingValue<string>(Settings.Address1Key);
            var address2Old = _repository.GetSettingValue<string>(Settings.Address2Key);
            var osOld = _repository.GetSettingValue<string>(Settings.OsKey);
            var pluginVersionOld = _repository.GetSettingValue<string>(Settings.AppVersionKey);
            if (pmsVersion != pmsVersionOld
                || pmsTypeOld != pmsType
                || address1Old != address1
                || address2Old != address2
                || osOld != Utils.GetOperatingSystemInfo()
                || pluginVersionOld != Utils.GetPluginVersion())
            {
                Logger.Info("Configuration changes detected. Updating plugin registration...");
                var machineId = _repository.GetSettingValue<string>(Settings.MachineIdKey);
                _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, address1, address2, pmsType, pmsVersion));
                Logger.Info("Plugin registration successfuly updated");
            }
        }

        public void UpdatePluginRegistration(string pmsVersion)
        {
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
                _chewsiApi.UpdatePluginRegistration(new UpdatePluginRegistrationRequest(machineId, address1Old, address2Old, pmsTypeOld, pmsVersion));
                Logger.Info("Plugin registration successfuly updated");
            }
        }

        public IDentalApi GetDentalApi()
        {
            var pmsType = _repository.GetSettingValue<string>(Api.Repository.Settings.PMS.TypeKey);
            if (pmsType != null)
            {
                Logger.Info("PMS type is " + pmsType);
                switch ((Api.Repository.Settings.PMS.Types)Enum.Parse(typeof(Api.Repository.Settings.PMS.Types), pmsType))
                {
                    case Api.Repository.Settings.PMS.Types.Dentrix:
                        return new DentrixApi();
                    case Api.Repository.Settings.PMS.Types.OpenDental:
                        return new OpenDentalApi.OpenDentalApi(_repository);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            Logger.Error("PMS system type is not set");
            return null;
        }
    }
}
