using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class DownloadItemViewModel : ViewModelBase
    {
        public DownloadItemViewModel(string ediDocumentId, string pdfReportDocumentId, string status, string postedDate)
        {
            EdiDocumentId = ediDocumentId;
            PdfReportDocumentId = pdfReportDocumentId;
            Status = status;
            PostedDate = postedDate;
        }

        public string EdiDocumentId { get; }

        public string PdfReportDocumentId { get; }

        public string Status { get; }

        public string PostedDate { get; }
    }
}
