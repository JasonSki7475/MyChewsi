using System.Windows.Input;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class PaymentsCalculationViewModel : ViewModelBase, IPaymentsCalculationViewModel
    {
        private ICommand _closeCommand;
        private string _chewsiMonthlyFee;
        private string _subscribersReoccuringMonthlyCharge;
        private string _totalProviderReimbursement;
        private string _totalProviderSubmittedCharge;
        private string _totalSubscriberCharge;
        private string _note;
        private bool _isVisible;

        #region CloseCommand
        public ICommand CloseCommand => _closeCommand ?? (_closeCommand = new RelayCommand(OnCloseCommandExecute));

        private void OnCloseCommandExecute()
        {
            Hide();
        }
        #endregion

        public void Show(CalculatedPaymentsDto m)
        {
            ChewsiMonthlyFee = m.ChewsiMonthlyFee;
            SubscribersReoccuringMonthlyCharge = m.SubscribersReoccuringMonthlyCharge;
            TotalProviderReimbursement = m.TotalProviderReimbursement;
            TotalProviderSubmittedCharge = m.TotalProviderSubmittedCharge;
            TotalSubscriberCharge = m.TotalSubscriberCharge;
            Note = m.Note;
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public string ChewsiMonthlyFee
        {
            get { return _chewsiMonthlyFee; }
            set
            {
                _chewsiMonthlyFee = value;
                RaisePropertyChanged(() => ChewsiMonthlyFee);
            }
        }

        public string SubscribersReoccuringMonthlyCharge
        {
            get { return _subscribersReoccuringMonthlyCharge; }
            set
            {
                _subscribersReoccuringMonthlyCharge = value;
                RaisePropertyChanged(() => SubscribersReoccuringMonthlyCharge);
            }
        }

        public string TotalProviderReimbursement
        {
            get { return _totalProviderReimbursement; }
            set
            {
                _totalProviderReimbursement = value;
                RaisePropertyChanged(() => TotalProviderReimbursement);
            }
        }

        public string TotalProviderSubmittedCharge
        {
            get { return _totalProviderSubmittedCharge; }
            set
            {
                _totalProviderSubmittedCharge = value;
                RaisePropertyChanged(() => TotalProviderSubmittedCharge);
            }
        }

        public string TotalSubscriberCharge
        {
            get { return _totalSubscriberCharge; }
            set
            {
                _totalSubscriberCharge = value;
                RaisePropertyChanged(() => TotalSubscriberCharge);
            }
        }

        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;
                RaisePropertyChanged(() => Note);
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                _isVisible = value;
                RaisePropertyChanged(() => IsVisible);
            }
        }
    }
}
