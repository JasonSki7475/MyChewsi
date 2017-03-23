using System.Collections.Generic;

namespace ChewsiPlugin.Api.Chewsi
{
    public class Request835DownloadsResponse : List<Request835DownloadsResponseItem>
    {
    }

    public class Request835DownloadsResponseItem
    {
        public string PostedDate { get; set; }
        public string Status { get; set; }
        public string EDI_835_Report { get; set; }
        public string EDI_835_EDI { get; set; }
    }
}
