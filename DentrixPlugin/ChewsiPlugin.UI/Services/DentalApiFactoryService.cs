using System;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI.Services
{
    internal class DentalApiFactoryService : IDentalApiFactoryService
    {
        private readonly IDialogService _dialogService;
        private readonly IRepository _repository;

        public DentalApiFactoryService(IDialogService dialogService, IRepository repository)
        {
            _dialogService = dialogService;
            _repository = repository;
        }

        public IDentalApi GetDentalApi(Settings.PMS.Types pmsType)
        {
            switch (pmsType)
            {
                case Settings.PMS.Types.Dentrix:
                    return new DentrixApi(_dialogService);
                case Settings.PMS.Types.OpenDental:
                    return new OpenDentalApi.OpenDentalApi(_repository, _dialogService);
                case Settings.PMS.Types.Eaglesoft:
                    return new EaglesoftApi.EaglesoftApi(_dialogService);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
