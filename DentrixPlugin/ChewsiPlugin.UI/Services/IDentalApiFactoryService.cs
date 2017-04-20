using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.UI.Services
{
    internal interface IDentalApiFactoryService
    {
        IDentalApi GetDentalApi(Settings.PMS.Types pmsType);
    }
}