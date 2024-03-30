using YoutubeDLSharp.Metadata;

namespace WebMediaTools.Models.Downloader
{
    public class DownloadInfoModel
    {
        public string message { get;set; }
        public List<MediaInfo>? entries { get; set; }
    }
}
