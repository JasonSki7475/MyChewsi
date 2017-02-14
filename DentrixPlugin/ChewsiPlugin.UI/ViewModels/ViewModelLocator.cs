using System;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.ViewModels.DialogService;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
            SimpleIoc.Default.Register<IDialogService, DialogService.DialogService>();
            SimpleIoc.Default.Register<IChewsiApi, ChewsiApi>();
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<IRepository, Repository>();
            SimpleIoc.Default.Register<IDentalApi>(LoadDentalApi);
        }

        private IDentalApi LoadDentalApi()
        {
            var repository = SimpleIoc.Default.GetInstance<IRepository>();
            var pmsType = repository.GetSettingValue<string>(Settings.PMS.TypeKey);
            if (pmsType != null)
            {
                Logger.Info("PMS type is " + pmsType);
                switch ((Settings.PMS.Types)Enum.Parse(typeof(Settings.PMS.Types), pmsType))
                {
                    case Settings.PMS.Types.Dentrix:
                        return new DentrixApi();
                    case Settings.PMS.Types.OpenDental:
                        return new OpenDentalApi.OpenDentalApi(repository);
                    default:
                        throw new ArgumentOutOfRangeException();
                }                
            }
            Logger.Error("PMS system type is not set");
            return null;
        }

        public MainViewModel MainViewModel
        {
            get { return ServiceLocator.Current.GetInstance<MainViewModel>(); }
        }
    }
}