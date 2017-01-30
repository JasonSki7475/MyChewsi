namespace DentrixPlugin.UI.ViewModels
{
    public class ClaimHistoryItemViewModel : ClaimItemViewModel
    {
        private string _status;

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaisePropertyChanged(() => Status);
            }
        }
    }
}