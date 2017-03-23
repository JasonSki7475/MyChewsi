namespace ChewsiPlugin.Api.Chewsi
{
    public class DownoadFileRequest
    {
        public string PostedOnDate { get; set; }
        public string TIN { get; set; }
        public string DocumentID { get; set; }
        public string DocumentType { get; set; }
    }

    public static class DownoadFileType
    {
        public static string Txt = "EDI";
        public static string Pdf = "Report";
    }
}
