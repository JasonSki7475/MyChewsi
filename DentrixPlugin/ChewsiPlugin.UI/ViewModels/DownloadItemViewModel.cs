using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    public class DownloadItemViewModel : ViewModelBase
    {
        private readonly string _ediDocumentId;
        private readonly string _pdfReportDocumentId;
        private readonly string _status;
        private readonly string _postedDate;

        public DownloadItemViewModel(string ediDocumentId, string pdfReportDocumentId, string status, string postedDate)
        {
            _ediDocumentId = ediDocumentId;
            _pdfReportDocumentId = pdfReportDocumentId;
            _status = status;
            _postedDate = postedDate;
        }

        public string EdiDocumentId
        {
            get { return _ediDocumentId; }
        }

        public string PdfReportDocumentId
        {
            get { return _pdfReportDocumentId; }
        }

        public string Status
        {
            get { return _status; }
        }

        public string PostedDate
        {
            get { return _postedDate; }
        }
    }
}
