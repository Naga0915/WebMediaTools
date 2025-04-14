using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WebMediaTools.Models.DownloadedList;

namespace WebMediaTools.Controllers.Downloader
{
    public class DownloadedListController : Controller
    {
        private readonly IConfiguration _configuration;
        public DownloadedListController(IConfiguration configuration)
        {
            this._configuration = configuration;
        }
        public IActionResult Index()
        {
            List<ListEntry> list = new List<ListEntry>();
            string path = _configuration["SaveDirectory"];
            string[] dirs = Directory.GetDirectories(path);
            foreach (var item in dirs)
            {
                var entry = new ListEntry();
                var info = new FileInfo(item);
                entry.guid = Path.GetFileName(item);
                foreach (var file in Directory.GetFiles(info.FullName))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.Extension == ".json") continue;
                    entry.name = fileInfo.Name;
                    entry.size = fileInfo.Length / 1024.0f / 1024.0f;
                }

                list.Add(entry);
            }
            var model = new DownloadedListModel();
            model.downloadedList = list;
            return View(model);
        }

        [Route("/DownloadedList/DeleteFile/{guid}")]
        public IActionResult DeleteFile(string guid)
        {
            string path = _configuration["SaveDirectory"];
            try
            {
                Directory.Delete(Path.Combine(path, guid), true);
                return Redirect("/DownloadedList/Index");
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public IActionResult DeleteFileAll()
        {
            string path = _configuration["SaveDirectory"];
            try
            {
                foreach(string dir in Directory.GetDirectories(path))
                {
                    Directory.Delete(dir, true);
                }
                return Redirect("/DownloadedList/Index");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
