using System;
using GalaSoft.MvvmLight;

namespace DentrixPlugin.UI.ViewModels
{
    public class ClaimItemViewModel : ViewModelBase
    {
        public DateTime Date { get; set; }
        public string ChewsiId { get; set; }
        public string Subscriber { get; set; }
        public string Provider { get; set; }
    }
}