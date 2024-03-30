using YoutubeDLSharp.Metadata;

namespace WebMediaTools.Models.Downloader
{
    public class MediaInfo
    {
        public VideoData data { get; set; }
        public List<FormatData> audioFormats { get; set; }
        public List<FormatData> videoFormats { get; set; }
        public List<FormatData> bothFormats { get; set; }

        public MediaInfo()
        {
            this.audioFormats = new List<FormatData>();
            this.videoFormats = new List<FormatData>();
            this.bothFormats = new List<FormatData>();
        }
    }
}
