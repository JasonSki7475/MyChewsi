using System;
using System.Collections.Generic;

namespace ChewsiPlugin.UI.Models
{
    internal class StatusLookupWaitItem
    {
        public StatusLookupWaitItem(string chewsiId, string providerId, DateTime dateTime, List<string> claimNumbers)
        {
            ChewsiId = chewsiId;
            ProviderId = providerId;
            DateTime = dateTime;
            ClaimNumbers = claimNumbers;
            Created = DateTime.UtcNow;
        }

        public string ChewsiId { get; private set; }
        public string ProviderId { get; private set; }
        public DateTime DateTime { get; private set; }
        public List<string> ClaimNumbers { get; private set; }
        public DateTime Created { get; private set; }
    }
}
