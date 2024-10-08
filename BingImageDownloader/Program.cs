﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
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
            //RepairArchivedImages();
            SaveArchivedImages();
            _logger.Info("Stop");
        }

      private static void RepairArchivedImages() {
         foreach(var img in _archivedImages) {
            if (string.IsNullOrWhiteSpace(img.Value.Market)) {
               int index = img.Value.UrlBase.IndexOf('_');
               img.Value.Market = img.Value.UrlBase.Substring(index + 1, 5);
            }

            if(string.IsNullOrWhiteSpace(img.Value.FileName)) 
               img.Value.FileName = CleanFileName(string.Format("{0}_{1}_{2}{3}", img.Value.Date, img.Value.Name, img.Value.Market, ".jpg"));
            
         }
      }

      private const string _bingUrl = @"http://www.bing.com";
        private const string _jsonFileName = "images.json";

        static void GetJson()
        {
            bool missing = true;
            bool otherMarket = true;
            bool duplicates = false;
            List<BingImage> images = new List<BingImage>();
            List<BingImage> downloadedImages = new List<BingImage>();
            String path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing");

            foreach (var market in _markets)
            {
                _logger.Info("Loading image list for {0}", market);
                if (otherMarket)
                {
                    images.AddRange(GetImagesList(0, 8, market));

                    if (missing)
                        images.AddRange(GetImagesList(7, 8, market));
                }
            }

            foreach (var i in images)
            {
                string url = string.Format("{0}{1}_{2}.jpg", _bingUrl, i.urlbase, GetSize());
                if (!_archivedImages.ContainsKey(i.GetImageName()))
                {
                    if (DownloadFile(url, path, i.GetImageFileName(duplicates)))
                    {
                        _archivedImages.Add(i.GetImageName(), new ArchivedImage(i));
                        downloadedImages.Add(i);
                    }
                }
            }

            
            if (downloadedImages.Count() > 0) {
               int index = new Random().Next(downloadedImages.Count());
               SetWallpaper(Path.Combine(path, downloadedImages[index].GetImageFileName(duplicates)));
               index = new Random().Next(downloadedImages.Count());
               SetLockScreen(Path.Combine(path, downloadedImages[index].GetImageFileName(duplicates)));
            }
            else {
               int index = new Random().Next(_archivedImages.Count()); 
               SetWallpaper(Path.Combine(path, _archivedImages.ElementAt(index).Value.FileName));
               index = new Random().Next(_archivedImages.Count());
               SetLockScreen(Path.Combine(path, _archivedImages.ElementAt(index).Value.FileName));
            }
        }

        /// <summary>
        /// Gets images list from bing servers
        /// </summary>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <param name="market"></param>
        /// <returns></returns>
        private static List<BingImage> GetImagesList(int index, int count, string market)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(GetUrl(index, count, market));
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                {
                    string json = new StreamReader(res.GetResponseStream()).ReadToEnd();
                    Bing b = JsonConvert.DeserializeObject<Bing>(json);
                    b.images.ForEach(x => x.market = market);
                    return b.images;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error geting images list. Index={index}, count={count}, market={market}");
            }
            return new List<BingImage>();
        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);


        public static void SetWallpaper(string file)
        {
            if(!File.Exists(file)) {
               _logger.Error(new FileNotFoundException("Wallpaper image doesn't exist", file), $"Wallpaper doesn't exist. Path={file}");
               return;
            }

            System.Drawing.Image img = System.Drawing.Image.FromFile(file);
            string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
            img.Save(tempPath, System.Drawing.Imaging.ImageFormat.Bmp);

            using(RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)) {
               key.SetValue(@"WallpaperStyle", 1.ToString()); //Centered
               key.SetValue(@"TileWallpaper", 0.ToString());
            }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

            using(RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true)) {
               key.SetValue(@"DesktopImagePath", file);
               key.SetValue(@"DesktopImageUrl", file);
               key.SetValue(@"DesktopImageStatus", 1);
               key.Close();
            }

            _logger.Info($"Set wallpaper image to {file}");
        }

        public static void SetLockScreen(string file) {
            if(!File.Exists(file)) {
               _logger.Error(new FileNotFoundException("Lockscreen image doesn't exist", file), $"Lockscreen doesn't exist. Path={file}");
               return;
            }

            using(RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true)) {
               key.SetValue(@"LockScreenImagePath", file);
               key.SetValue(@"LockScreenImageUrl", file);
               key.SetValue(@"LockScreenImageStatus", 1);
               key.Close();
               _logger.Info($"Set lockscreen image to {file}");
            }
        }

        static object GetSize()
        {
            return "1920x1080";
        }

        static bool DownloadFile(string url, string path, string name)
        {
            bool ret = false;
            string filePath = Path.Combine(path, name);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Info($"Downloading {url}");
                    WebClient wb = new WebClient();
                    wb.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");
                    wb.DownloadFile(url, filePath);
                    ret = true;
                }
                else
                    _logger.Info($"Skipped {url}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Wystąpił błąd w trakcie pobierania pliku {0}", filePath);
                File.Delete(filePath);
            }
            return ret;
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
                _archivedImages = (Dictionary<string, ArchivedImage>)serializer.Deserialize(file, typeof(Dictionary<string, ArchivedImage>));
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
        private static Dictionary<string, ArchivedImage> _archivedImages = new Dictionary<string, ArchivedImage>();
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
                // Tak było po staremu
                // EnglemannSpruceForest_ROW12845047121_1920x1080.jpg
                //string name = Path.GetFileName(this.url);
                
                // Teraz jest tak
                // /th?id=OHR.AerialKluaneNP_JA-JP2035742573_1920x1080.jpg&rf=LaDigue_1920x1080.jpg&pid=hp
                Uri u = new Uri(_bingUrl + this.url);
                var q = HttpUtility.ParseQueryString(u.Query);
                string name = q["id"];

                return name.Substring(0, name.IndexOf('_'));
            }  

            public string GetImageFileName(bool dupl)
            {
                string ret = string.Empty;
                // Tak było po staremu
                // EnglemannSpruceForest_ROW12845047121_1920x1080.jpg
                // string name = Path.GetFileName(this.url);

                // Teraz jest tak
                // /th?id=OHR.AerialKluaneNP_JA-JP2035742573_1920x1080.jpg&rf=LaDigue_1920x1080.jpg&pid=hp
                Uri u = new Uri(_bingUrl + this.url);
                var q = HttpUtility.ParseQueryString(u.Query);
                string name = q["id"];

                int index = name.IndexOf('_');
                string imageName = name.Substring(0, index);
                string rest = name.Substring(index + 1);
                if (dupl)
                    ret = string.Format("{0}_{1}{2}", this.startdate, imageName, rest);
                else
                    ret = string.Format("{0}_{1}_{2}{3}", this.startdate, imageName, this.market, Path.GetExtension(name));

                return CleanFileName(ret.Trim());
            }
        }

        public class ArchivedImage
        {
            public string Name { get; set; }
            public string Market { get; set; }
            public string Description { get; set; }
            public string Date { get; set; }
            public string UrlBase { get; set; }
            public string FileName {get; set;}

            public ArchivedImage() { }

            public ArchivedImage(BingImage b)
            {
                Name = b.GetImageName();
                Market = b.market;
                Description = b.copyright;
                Date = b.startdate;
                UrlBase = b.urlbase;
                FileName = b.GetImageFileName(false);
            }
      }
    }
}
