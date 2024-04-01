using YoutubeDLSharp.Options;

namespace WebMediaTools.Models.Downloader
{
    public class DownloadTaskModel
    {
        public string url { get; set; }
        public string format { get; set; }
        public string container { get; set; }
        public string guid { get; set; }
    }
}
