using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using WebMediaTools.Models.Downloader;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace WebMediaTools.Controllers.Downloader
{
    public class DownloaderController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DownloaderController> _logger;
        public DownloaderController(IConfiguration configuration, ILogger<DownloaderController> logger)
        {
            this._configuration = configuration;
            this._logger = logger;
        }

        public IActionResult Index()
        {
            ViewBag.Message = "This is downloader controller!";
            ViewData["now"] = DateTime.Now;
            return View();
        }

        [HttpPost]
        public IActionResult Download(DownloadInfoModel model)
        {
            return Ok(model);
        }

        [HttpPost]
        public async Task<IActionResult> GetFormats(string url)
        {
            var result = new DownloadInfoModel();
            await YoutubeDLSharp.Utils.DownloadBinaries(true, Directory.GetCurrentDirectory());
            try
            {
                if (!Directory.Exists(_configuration["SaveDirectory"])) throw new FileNotFoundException("media directry not found");
                FileInfo binInfo = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), YoutubeDLSharp.Utils.YtDlpBinaryName));
                if (!binInfo.Exists) throw new FileNotFoundException("yt-dlp binary does not exists");
                binInfo.IsReadOnly = false;
            }
            catch (Exception ex)
            {
                result.message = ex.Message + ex.TargetSite;
                return PartialView("_ResultPartial", result);
            }
            var ytdl = new YoutubeDL();
            ytdl.OutputFolder = _configuration["SaveDirectory"];
            ytdl.RestrictFilenames = true;
            ytdl.PythonInterpreterPath = Path.Combine(_configuration["PythonDirectory"], _configuration["PythonName"]);
            try
            {
                var res = await ytdl.RunVideoDataFetch(url, flat: false);
                if (!res.Success)
                {
                    StringBuilder builder = new StringBuilder();
                    foreach (string str in res.ErrorOutput)
                    {
                        builder.AppendLine(str);
                    }
                    return PartialView("_ResultPartial", result);
                }
                result.message = "ok";
                MediaInfo mediaInfo;
                switch (res.Data.ResultType)
                {
                    case YoutubeDLSharp.Metadata.MetadataType.Video:
                        if (res.Data.Formats == null) break;
                        mediaInfo = new MediaInfo();
                        mediaInfo.data = res.Data;
                        foreach (var format in res.Data.Formats)
                        {
                            if (format.AudioCodec == "none" && format.VideoCodec == "none") continue;
                            if (format.AudioCodec != "none" && format.VideoCodec == "none")
                            {
                                mediaInfo.audioFormats.Add(format);
                            }
                            else if (format.AudioCodec == "none")
                            {
                                mediaInfo.videoFormats.Add(format);
                            }
                            else
                            {
                                mediaInfo.bothFormats.Add(format);
                            }
                        }
                        result.entries = new List<MediaInfo> { mediaInfo };
                        break;
                    case YoutubeDLSharp.Metadata.MetadataType.Playlist:
                        var mediaInfoList = new List<MediaInfo>();
                        foreach (var data in res.Data.Entries)
                        {
                            mediaInfo = new MediaInfo();
                            mediaInfo.data = data;
                            foreach (var format in data.Formats)
                            {
                                if (format.AudioCodec == "none" && format.VideoCodec == "none") continue;
                                if (format.AudioCodec != "none" && format.VideoCodec == "none")
                                {
                                    mediaInfo.audioFormats.Add(format);
                                }
                                else if (format.AudioCodec == "none")
                                {
                                    mediaInfo.videoFormats.Add(format);
                                }
                                else
                                {
                                    mediaInfo.bothFormats.Add(format);
                                }
                            }
                            mediaInfoList.Add(mediaInfo);
                        }
                        result.entries = mediaInfoList;
                        break;
                }
                return PartialView("_ResultPartial", result);
            }
            catch (Exception ex)
            {
                result.message = ex.Message + ex.TargetSite;
                return PartialView("_ResultPartial", result);
            }
        }
    }
}
