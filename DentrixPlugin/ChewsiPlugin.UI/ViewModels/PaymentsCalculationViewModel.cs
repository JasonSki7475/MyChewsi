using System.Windows.Input;
using ChewsiPlugin.Api.Chewsi;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NLog;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class PaymentsCalculationViewModel : ViewModelBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private ICommand _saveCommand;
        
        #region SaveCommand
        public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(OnSaveCommandExecute));

        private void OnSaveCommandExecute()
        {

        }
        #endregion   

        public void Init(CalculatedOrthoPaymentsResponse)
    }
}
