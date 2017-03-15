using System;

namespace ChewsiPlugin.OpenDentalApi.DTO
{
    [Serializable]
    internal class InsSubInfo
    {
        /// <summary>
        /// Chewsi Id: "...usually SSN, but can also be changed by user.  No dashes. Not allowed to be blank"
        /// </summary>
        public string SubscriberID { get; set; }

        /// <summary>
        /// PatNum
        /// </summary>
        public long Subscriber { get; set; }

        public long InsSubNum { get; set; }
    }
}
