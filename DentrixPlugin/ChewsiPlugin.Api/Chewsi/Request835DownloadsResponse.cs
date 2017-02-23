using System.Collections.Generic;

namespace ChewsiPlugin.Api.Chewsi
{
    public class Request835DownloadsResponse : List<Request835DownloadsResponseItem>
    {
    }

    public class Request835DownloadsResponseItem
    {
        public string PostedOnDate { get; set; }
        public string url { get; set; }
        public string Payee_ID { get; set; }
        public string File_835_Report_url { get; set; }
        public string File_835_EDI_url { get; set; }
    }
}
