using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace TSviewCloudConfig
{
    public static class Config
    {
        // temporary
        static public bool ApplicationExit = false;
        static public TSviewCloud.FormLog Log = new TSviewCloud.FormLog();
        internal static string MasterPassword;
        internal static bool debug;

        // general
        public static readonly string Version = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion.ToString();
        public static bool SaveGZConfig = true;
        public static double UploadLimit = double.PositiveInfinity;
        public static double DownloadLimit = double.PositiveInfinity;
        public static int ParallelDownload = 3;
        public static int ParallelUpload = 3;
        public static int UploadBufferSize = 16 * 1024 * 1024;
        public static int DownloadBufferSize = 16 * 1024 * 1024;

        private static string GetFileSystemPath(Environment.SpecialFolder folder)
        {
            string path = string.Format(@"{0}\{1}\{2}",
              Environment.GetFolderPath(folder),  
              Application.CompanyName,            
              Application.ProductName);           

            lock (typeof(Application))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            return path;
        }
        private static string GetConfigPath(string basepath)
        {
            string path = Path.Combine(basepath, "Config");

            lock (typeof(Application))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            return path;
        }



        private static readonly bool IsInstalled = File.Exists(Path.Combine(Application.StartupPath, "_installed.txt"));
        private static readonly string _config_basepath = (IsInstalled) ? GetFileSystemPath(Environment.SpecialFolder.ApplicationData) : "";
        public static string Config_BasePath
        {
            get { return _config_basepath; }
        }
        private static readonly string _filepath = Path.Combine(GetConfigPath(_config_basepath), Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".xml");
        private static string filepath
        {
            get { return _filepath; }
        }



        static Config()
        {
            var serializer = new DataContractSerializer(typeof(Savedata));
            try
            {
                using (var xmlr = XmlReader.Create(filepath))
                {
                    var data = (Savedata)serializer.ReadObject(xmlr);

                    if (data.SaveCacheCompressed != true)
                        SaveGZConfig = data.SaveCacheCompressed;
                    if (data.UploadBandwidthLimit != default(double))
                        UploadLimit = data.UploadBandwidthLimit;
                    if (data.DownloadBandwidthLimit != default(double))
                        DownloadLimit = data.DownloadBandwidthLimit;
                    if (data.ParallelDownload != default(int))
                        ParallelDownload = data.ParallelDownload;
                    if (data.ParallelUpload != default(int))
                        ParallelUpload = data.ParallelUpload;
                    if (data.DownloadBufferSize != default(int))
                        DownloadBufferSize = data.DownloadBufferSize;
                    if (data.UploadBufferSize != default(int))
                        UploadBufferSize = data.UploadBufferSize;

                    if (data.FFplayer != default(SavedataFFplayer))
                    {
                        if(data.FFplayer.FFmoduleKeybinds != null)
                        {
                            if (data.FFplayer.FFmoduleKeybinds.Count() >= ConfigFFplayer.FFmoduleKeybinds.Count())
                                ConfigFFplayer.FFmoduleKeybinds = data.FFplayer.FFmoduleKeybinds;
                            else
                                data.FFplayer.FFmoduleKeybinds.ToList().ForEach(x => ConfigFFplayer.FFmoduleKeybinds[x.Key] = x.Value);

                        }
                        if (data.FFplayer.FontFilePath != default(string))
                            ConfigFFplayer.FontFilePath = data.FFplayer.FontFilePath;
                        if (data.FFplayer.FontPtSize != default(int))
                            ConfigFFplayer.FontPtSize = data.FFplayer.FontPtSize;
                        if (data.FFplayer.AutoResize != true)
                            ConfigFFplayer.AutoResize = data.FFplayer.AutoResize;

                        if (data.FFplayer.fullscreen != default(bool))
                            ConfigFFplayer.fullscreen = data.FFplayer.fullscreen;
                        if (data.FFplayer.display != default(bool))
                            ConfigFFplayer.display = data.FFplayer.display;
                        if (data.FFplayer.volume != default(double))
                            ConfigFFplayer.volume = data.FFplayer.volume;
                        if (data.FFplayer.mute != default(bool))
                            ConfigFFplayer.mute = data.FFplayer.mute;
                        if (data.FFplayer.width != default(int))
                            ConfigFFplayer.width = data.FFplayer.width;
                        if (data.FFplayer.hight != default(int))
                            ConfigFFplayer.hight = data.FFplayer.hight;
                        if (data.FFplayer.x != default(int))
                            ConfigFFplayer.x = data.FFplayer.x;
                        if (data.FFplayer.y != default(int))
                            ConfigFFplayer.y = data.FFplayer.y;

                    }
                }
            }
            catch (Exception)
            {
                Save();
            }
        }
        public static void Save()
        {
            lock (filepath)
            {
                var serializer = new DataContractSerializer(typeof(Savedata));
                using (var xmlw = XmlWriter.Create(filepath, new XmlWriterSettings { Indent = true }))
                {
                    var ffdata = new SavedataFFplayer
                    {
                        FFmoduleKeybinds = ConfigFFplayer.FFmoduleKeybinds,
                        FontFilePath = ConfigFFplayer.FontFilePath,
                        FontPtSize = ConfigFFplayer.FontPtSize,
                        AutoResize = ConfigFFplayer.AutoResize,
                        fullscreen = ConfigFFplayer.fullscreen,
                        display = ConfigFFplayer.display,
                        volume = ConfigFFplayer.volume,
                        mute = ConfigFFplayer.mute,
                        width = ConfigFFplayer.width,
                        hight = ConfigFFplayer.hight,
                        x = ConfigFFplayer.x,
                        y = ConfigFFplayer.y,
                    };
                    var data = new Savedata
                    {
                        Version = Version,
                        SaveCacheCompressed = SaveGZConfig,
                        UploadBandwidthLimit = UploadLimit,
                        DownloadBandwidthLimit = DownloadLimit,
                        ParallelDownload = ParallelDownload,
                        ParallelUpload = ParallelUpload,
                        DownloadBufferSize = DownloadBufferSize,
                        UploadBufferSize = UploadBufferSize,
                        FFplayer = ffdata,
                    };
                    serializer.WriteObject(xmlw, data);
                }
            }
        }
    }

    [DataContract]
    class Savedata
    {
        [DataMember]
        public string Version;
        [DataMember]
        public bool SaveCacheCompressed;
        [DataMember]
        public double UploadBandwidthLimit;
        [DataMember]
        public double DownloadBandwidthLimit;
        [DataMember]
        public int ParallelDownload;
        [DataMember]
        public int ParallelUpload;
        [DataMember]
        public int UploadBufferSize;
        [DataMember]
        public int DownloadBufferSize;

        [DataMember]
        public SavedataFFplayer FFplayer;
    }
}