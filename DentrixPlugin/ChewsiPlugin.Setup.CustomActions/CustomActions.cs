using System.Collections.Generic;
using System.Linq;
using ChewsiPlugin.Api.Dentrix;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;

namespace ChewsiPlugin.Setup.CustomActions
{
    public static class CustomActions
    {
        private static readonly List<IDentalApi> DentalApis = new List<IDentalApi>
            {
                new DentrixApi(),
                new OpenDentalApi.OpenDentalApi(new Repository())
            }; 

        public static List<string> GetInstalledPMS()
        {
            string path;
            return DentalApis
                .Where(m => m.IsInstalled(out path))
                .Select(m => m.Name)
                .ToList();
        }

        public static void SetCurrentPMS(string name, string path)
        {
            var repository = new Repository();
            repository.SaveSetting(Settings.PMS.TypeKey, DentalApis.First(m => m.Name == name).Type);
            repository.SaveSetting(Settings.PMS.PathKey, path);
            // repository.SaveSetting(Settings.PMS.TypeKey, Settings.PMS.Types.OpenDental);
            // repository.SaveSetting(Settings.PMS.PathKey, @"C:\Program Files (x86)\Open Dental\");
        }
    }
}
