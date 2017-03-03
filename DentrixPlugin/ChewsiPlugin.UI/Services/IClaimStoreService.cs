using System;
using System.Collections.ObjectModel;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.UI.ViewModels;

namespace ChewsiPlugin.UI.Services
{
    public interface IClaimStoreService
    {
        void RequestStatusLookup(Provider provider);
        void RefreshAppointments();
        ObservableCollection<ClaimItemViewModel> ClaimItems { get; }
        bool IsProcessingPayment { get; }
        void DeleteAppointment(string chewsiId, DateTime date);
        void DeleteOldAppointments();
    }
}
