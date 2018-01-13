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
            //AllocConsole();

            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            GetJson();

        }

        private const string _bingUrl = @"http://www.bing.com";

        static void GetJson()
        {
            bool missing = true;
            bool otherMarket = true;
            bool duplicates = false;
            List<BingImage> images = new List<BingImage>();

            String path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Bing");

            //var req = (HttpWebRequest)WebRequest.Create(GetUrl());
            //var res = (HttpWebResponse)req.GetResponse();
            //string json = new StreamReader(res.GetResponseStream()).ReadToEnd();
            //Bing b = JsonConvert.DeserializeObject<Bing>(json);
            //images.AddRange(b.images);

            //if (missing)
            //{
            //    req = (HttpWebRequest)WebRequest.Create(GetUrl(7));
            //    res = (HttpWebResponse)req.GetResponse();
            //    json = new StreamReader(res.GetResponseStream()).ReadToEnd();
            //    b = JsonConvert.DeserializeObject<Bing>(json);
            //    images.AddRange(b.images);
            //}

            foreach (var market in markets)
            {
                if (otherMarket)
                {
                    var req = (HttpWebRequest)WebRequest.Create(GetUrl(0, 8, market));
                    var res = (HttpWebResponse)req.GetResponse();
                    var json = new StreamReader(res.GetResponseStream()).ReadToEnd();
                    var b = JsonConvert.DeserializeObject<Bing>(json);
                    images.AddRange(b.images);

                    if (missing)
                    {
                        req = (HttpWebRequest)WebRequest.Create(GetUrl(7, 8, market));
                        res = (HttpWebResponse)req.GetResponse();
                        json = new StreamReader(res.GetResponseStream()).ReadToEnd();
                        b = JsonConvert.DeserializeObject<Bing>(json);
                        images.AddRange(b.images);
                    }
                }
            }


            foreach (var i in images)
            {
                string url = string.Format("{0}{1}_{2}.jpg", _bingUrl, i.urlbase, GetSize());
                DownloadFile(url, path, GetImageName(i, duplicates));
            }

            int index = new Random().Next(images.Count());
            SetWallpaper(Path.Combine(path, CleanFileName(GetImageName(images[index], duplicates))));

        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);


        public static void SetWallpaper(string file)
        {
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

        static string GetImageName(BingImage i, bool dupl)
        {
            //EnglemannSpruceForest_ROW12845047121_1920x1080.jpg
            string name = Path.GetFileName(i.url);
            int index = name.IndexOf('_');
            string imageName = name.Substring(0, index);
            string rest = name.Substring(index + 1);
            if (dupl)
                return string.Format("{0}_{1}{2}", imageName, i.startdate, rest);
            else
                return string.Format("{0}_{1}{2}", imageName, i.startdate, Path.GetExtension(i.url));
        }

        static void DownloadFile(string url, string path, string name)
        {
            string filePath = Path.Combine(path, CleanFileName(name?.Trim()));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //if (filePath.Length >= 250)
            //   filePath = filePath.Substring(0, 249);

            //filePath += ".mp3";

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Info("Pobieram {0}", url);
                    WebClient wb = new WebClient();
                    wb.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.33 Safari/537.36");
                    wb.DownloadFile(url, filePath);
                }
                else
                    _logger.Info("Pomijam {0}", url);
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

        static string[] markets = new string[12] { "EN-GB", "PT-BR", "EN-CA", "FR-CA", "EN-US", "EN-WW", "EN-AU", "JA-JP", "ZH-CN", "EN-IN", "DE-DE", "FR-FR" };
        private static Logger _logger = LogManager.GetCurrentClassLogger();

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
        }

    }
}
