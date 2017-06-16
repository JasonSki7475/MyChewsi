using System;

namespace ChewsiPlugin.Api.Repository
{
    public class DeletedStatus
    {
        public string Id { get; set; }
        public string ProviderId { get; set; }
        public string ChewsiId { get; set; }
        public DateTime Date { get; set; }
    }
}
