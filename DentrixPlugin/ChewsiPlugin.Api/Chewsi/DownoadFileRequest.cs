namespace ChewsiPlugin.Api.Chewsi
{
    public class DownoadFileRequest
    {
        public string postedOnDate { get; set; }
        public string url { get; set; }
        public string payee_ID { get; set; }
        public string FileType { get; set; }
    }

    public static class DownoadFileType
    {
        public static string Txt = "835txt";
        public static string Pdf = "835pdf";
    }
}
