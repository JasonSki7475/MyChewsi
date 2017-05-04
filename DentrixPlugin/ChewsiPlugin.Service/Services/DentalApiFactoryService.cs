using System;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Service.Services
{
    internal class DentalApiFactoryService : IDentalApiFactoryService
    {
        private readonly IRepository _repository;

        public DentalApiFactoryService(IRepository repository)
        {
            _repository = repository;
        }

        public IDentalApi GetDentalApi(Settings.PMS.Types pmsType)
        {
            switch (pmsType)
            {
                case Settings.PMS.Types.Dentrix:
                    return new DentrixApi();
                case Settings.PMS.Types.OpenDental:
                    return new OpenDentalApi.OpenDentalApi(_repository);
                case Settings.PMS.Types.Eaglesoft:
                    return new EaglesoftApi.EaglesoftApi();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}