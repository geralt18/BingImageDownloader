using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;

namespace BingImageDownloader
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            _logger.Info("Start");
            _jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _jsonFileName);
            //AllocConsole();
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            LoadArchivedImages();
            GetJson();
            SaveArchivedImages();
            _logger.Info("Stop");
        }

        private const string _bingUrl = @"http://www.bing.com";
        private const string _jsonFileName = "images.json";

        static void GetJson()
        {
            bool missing = true;
            bool otherMarket = true;
            bool duplicates = false;
            List<BingImage> images = new List<BingImage>();
            String path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing");

            foreach (var market in _markets)
            {
                _logger.Info("Loading image list for {0}", market);
                if (otherMarket)
                {
                    var req = (HttpWebRequest)WebRequest.Create(GetUrl(0, 8, market));
                    var res = (HttpWebResponse)req.GetResponse();
                    var json = new StreamReader(res.GetResponseStream()).ReadToEnd();
                    var b = JsonConvert.DeserializeObject<Bing>(json);
                    b.images.ForEach(x => x.market = market);
                    images.AddRange(b.images);

                    if (missing)
                    {
                        req = (HttpWebRequest)WebRequest.Create(GetUrl(7, 8, market));
                        res = (HttpWebResponse)req.GetResponse();
                        json = new StreamReader(res.GetResponseStream()).ReadToEnd();
                        b = JsonConvert.DeserializeObject<Bing>(json);
                        b.images.ForEach(x => x.market = market);
                        images.AddRange(b.images);
                    }
                }
            }


            foreach (var i in images)
            {
                string url = string.Format("{0}{1}_{2}.jpg", _bingUrl, i.urlbase, GetSize());
                if (!_archivedImages.Exists(x => x.Name.Equals(i.GetImageName(), StringComparison.InvariantCultureIgnoreCase)))
                {
                    DownloadFile(url, path, i.GetImageFileName(duplicates));
                    _archivedImages.Add(new ArchivedImage(i, null));
                }
            }

            int index = new Random().Next(images.Count());
            SetWallpaper(Path.Combine(path, CleanFileName(images[index].GetImageFileName(duplicates))));
        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);


        public static void SetWallpaper(string file)
        {
            if (!File.Exists(file))
                return;

            System.Drawing.Image img = System.Drawing.Image.FromFile(file);
            string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
            img.Save(tempPath, System.Drawing.Imaging.ImageFormat.Bmp);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            //if (style == Style.Stretched)
            //{
            //   key.SetValue(@"WallpaperStyle", 2.ToString());
            //   key.SetValue(@"TileWallpaper", 0.ToString());
            //}

            //if (style == Style.Centered)
            //{
            key.SetValue(@"WallpaperStyle", 1.ToString());
            key.SetValue(@"TileWallpaper", 0.ToString());
            //}

            //if (style == Style.Tiled)
            //{
            //   key.SetValue(@"WallpaperStyle", 1.ToString());
            //   key.SetValue(@"TileWallpaper", 1.ToString());
            //}

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        static object GetSize()
        {
            return "1920x1080";
        }



        static void DownloadFile(string url, string path, string name)
        {
            string filePath = Path.Combine(path, CleanFileName(name?.Trim()));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Info("Donwloading {0}", url);
                    WebClient wb = new WebClient();
                    wb.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");
                    wb.DownloadFile(url, filePath);
                }
                else
                    _logger.Info("Skipped {0}", url);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Wystąpił błąd w trakcie pobierania pliku {0}", filePath);
                File.Delete(filePath);
            }
        }

        static string CleanFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            return name;
        }

        static string GetUrl(int index = 0, int count = 8, string market = "en-Us")
        {
            if (index > 7)
                index = 7;
            if (count > 8)
                count = 8;

            return string.Format(@"http://www.bing.com/HPImageArchive.aspx?format=js&idx={0}&n={1}&mkt={2}", index, count, market);
        }

        static void LoadArchivedImages()
        {
            if (!File.Exists(_jsonFilePath))
                return;
            _logger.Trace("Loading archived images list");
            using (StreamReader file = File.OpenText(_jsonFilePath))
            {
                JsonSerializer serializer = new JsonSerializer();
                _archivedImages = (List<ArchivedImage>)serializer.Deserialize(file, typeof(List<ArchivedImage>));
            }
        }

        static void SaveArchivedImages()
        {
            _logger.Trace("Saving archived images list");
            using (StreamWriter file = File.CreateText(_jsonFilePath))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, _archivedImages);
            }
        }

        static string[] _markets = new string[12] {
            "EN-US",
            "DE-DE",
            "EN-AU",
            "EN-CA",
            "EN-GB",
            "EN-IN",
            "EN-WW",
            "FR-CA",
            "FR-FR",
            "JA-JP",
            "PT-BR",
            "ZH-CN" };

        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private static List<ArchivedImage> _archivedImages = new List<ArchivedImage>();
        private static string _jsonFilePath;

        public class Bing
        {
            public List<BingImage> images;
            public ToolTip tooltips;
        }

        public class BingTooltip
        {
            public string loading;
            public string previous;
            public string next;
            public string walle;
            public string walls;
        }

        public class BingImage
        {
            public string startdate { get; set; }
            public string fullstartdate { get; set; }
            public string enddate { get; set; }
            public string url { get; set; }
            public string urlbase { get; set; }
            public string copyright { get; set; }
            public string copyrightlink { get; set; }
            public string quiz { get; set; }
            public string market { get; set; }

            public string GetImageName()
            {
                //EnglemannSpruceForest_ROW12845047121_1920x1080.jpg
                string name = Path.GetFileName(this.url);
                return name.Substring(0, name.IndexOf('_'));
            }

            public string GetImageFileName(bool dupl)
            {
                //EnglemannSpruceForest_ROW12845047121_1920x1080.jpg
                string name = Path.GetFileName(this.url);
                int index = name.IndexOf('_');
                string imageName = name.Substring(0, index);
                string rest = name.Substring(index + 1);
                if (dupl)
                    return string.Format("{0}_{1}{2}", this.startdate, imageName, rest);
                else
                    return string.Format("{0}_{1}_{2}{3}", this.startdate, imageName, this.market, Path.GetExtension(this.url));
            }
        }

        public class ArchivedImage
        {
            public string Name { get; set; }
            public string Market { get; set; }
            public string Description { get; set; }
            public string Date { get; set; }
            public string UrlBase { get; set; }

            public ArchivedImage() { }

            public ArchivedImage(BingImage b, string market)
            {
                Name = b.GetImageName();
                Market = market;
                Description = b.copyright;
                Date = b.startdate;
                UrlBase = b.urlbase;
            }
        }


    }
}
