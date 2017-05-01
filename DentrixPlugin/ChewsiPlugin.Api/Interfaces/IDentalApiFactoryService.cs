using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Api.Interfaces
{
    public interface IDentalApiFactoryService
    {
        IDentalApi GetDentalApi(Settings.PMS.Types pmsType);
    }
}