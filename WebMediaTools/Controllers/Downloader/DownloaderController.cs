using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using WebMediaTools.Models.Downloader;
using WebMediaTools.SharedServices;
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
        private readonly ISharedDictionaryService _sharedDictionaryService;
        private readonly ConcurrentDictionary<string, float> downloadProgressDic;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> downloadCancelTokenDic;
        private readonly ConcurrentDictionary<string, bool> downloadingDic;
        private readonly int minWaitTime;
        private readonly int maxWaitTime;

        public DownloaderController(IConfiguration configuration, ILogger<DownloaderController> logger, ISharedDictionaryService sharedDictionaryService)
        {
            this._configuration = configuration;
            this._logger = logger;
            this._sharedDictionaryService = sharedDictionaryService;
            this.downloadProgressDic = _sharedDictionaryService.downloadProgressDic;
            this.downloadCancelTokenDic = _sharedDictionaryService.downloadCancelTokenDic;
            this.downloadingDic = _sharedDictionaryService.downloadingDic;
            var min = this._configuration.GetValue<int>("RandomMinWaitTime", -1);
            var max = this._configuration.GetValue<int>("RandomMaxWaitTime", -1);
            if (min < 0 || max < 0) throw new JsonException("invalid RandomMinWaitTime or RandomMaxWaitTime");
            if (max < min) throw new JsonException("max wait time is longer than min wait time");
            this.minWaitTime = min;
            this.maxWaitTime = max;
        }

        public IActionResult Index()
        {
            ViewBag.Message = "This is downloader controller!";
            ViewData["now"] = DateTime.Now;
            return View();
        }

        [Route("/Downloader/DownloadFile/{id}")]
        public async Task<IActionResult> DownloadFile(string id)
        {
            if (downloadingDic.ContainsKey(id)) return NotFound("file is still downloading");
            string path = Path.Combine(_configuration["SaveDirectory"], id);
            string[] files = Directory.GetFiles(path);
            if (files.Count() < 1) return NotFound("file not found");
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            string? mime = null;
            foreach(var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.Extension == ".json") continue;

                contentTypeProvider.TryGetContentType(file, out mime);
                if (mime == null) mime = "application/octet-stream";
                return PhysicalFile(fileInfo.FullName, mime, fileInfo.Name);
            }
            return BadRequest("file not found");
        }

        public float GetProgress(string guid)
        {
            float progress;
            if(!downloadProgressDic.TryGetValue(guid, out progress)) progress = -1.0f;
            return progress;
        }

        public IActionResult StopDL(string guid)
        {
            var rnd = new Random();
            CancellationTokenSource? token;
            if (!downloadCancelTokenDic.TryGetValue(guid, out token)) return NotFound("invalid guid");
            if (token.IsCancellationRequested) return BadRequest("already cancelled");
            token.Cancel();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Download(DownloadTaskModel model)
        {
            //add progress to dictionary
            if (!downloadProgressDic.TryAdd(model.guid, 0.0f)) return BadRequest("task already exists");

            //add progress callback
            var progress = new Progress<DownloadProgress>(p =>
            {
                float oldValue = -1.0f;
                downloadProgressDic.TryGetValue(model.guid, out oldValue);
                if (oldValue == -1.0f) return;
                downloadProgressDic.TryUpdate(model.guid, p.Progress, oldValue);
            });

            //add token to dictionary
            var ct = new CancellationTokenSource();
            downloadCancelTokenDic.TryAdd(model.guid, ct);

            if (!downloadingDic.TryAdd(model.guid, true)) return BadRequest("still downloading");

            //downloading
            await YoutubeDLSharp.Utils.DownloadBinaries(true, Directory.GetCurrentDirectory());
            if (!Directory.Exists(_configuration["SaveDirectory"])) throw new FileNotFoundException("save directry not found");
            FileInfo binInfo = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), YoutubeDLSharp.Utils.YtDlpBinaryName));
            if (!binInfo.Exists) throw new FileNotFoundException("yt-dlp binary does not exists");
            binInfo.IsReadOnly = false;

            var ytdlp = new YoutubeDL();
            ytdlp.PythonInterpreterPath = Path.Combine(_configuration["PythonDirectory"], _configuration["PythonName"]);
            string outputPath = Path.Combine(_configuration["SaveDirectory"], model.guid);
            Directory.CreateDirectory(outputPath);
            ytdlp.OutputFolder = outputPath;

            AudioConversionFormat audioFormat;
            VideoRecodeFormat videoFormat;
            if (Enum.TryParse(model.container, out audioFormat))
            {
                var option = YoutubeDLSharp.Options.OptionSet.Default;
                //option.Format = model.format;
                option.WindowsFilenames = true;
                await ytdlp.RunAudioDownload(model.url, format: audioFormat, progress: progress, ct: ct.Token);
            }
            else if (Enum.TryParse(model.container, out videoFormat))
            {
                var option = YoutubeDLSharp.Options.OptionSet.Default;
                option.Format = model.format;
                option.WindowsFilenames = true;
                await ytdlp.RunVideoDownload(model.url, format: model.format, overrideOptions: option, progress: progress, ct: ct.Token, recodeFormat: videoFormat);
            }
            else
            {
                return BadRequest("invalid container");
            }

            var result = new UserDownloadModel();
            result.url = "/Downloader/DownloadFile/" + model.guid;

            downloadProgressDic.TryRemove(model.guid, out _);
            downloadCancelTokenDic.TryRemove(model.guid, out _);
            downloadingDic.TryRemove(model.guid, out _);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> GetFormats(string url)
        {
            var result = new DownloadInfoModel();
            await YoutubeDLSharp.Utils.DownloadBinaries(true, Directory.GetCurrentDirectory());
            try
            {
                if (!Directory.Exists(_configuration["SaveDirectory"])) throw new FileNotFoundException("save directry not found");
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
