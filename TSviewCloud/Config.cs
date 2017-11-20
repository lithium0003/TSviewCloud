using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
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
        internal static bool debug;

        // general
        public static readonly string Version = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion.ToString();
        public static bool SaveGZConfig = true;
        public static bool SaveEncrypted = false;
        public static double UploadLimit = double.PositiveInfinity;
        public static double DownloadLimit = double.PositiveInfinity;
        public static int ParallelDownload = 3;
        public static int ParallelUpload = 3;
        public static int UploadBufferSize = 16 * 1024 * 1024;
        public static int DownloadBufferSize = 16 * 1024 * 1024;
        public static System.Drawing.Point? Main_Location;
        public static System.Drawing.Size? Main_Size;

        public static bool LogToFile
        {
            get { return Log.LogToFile; }
            set { Log.LogToFile = value; }
        }

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
        private static string GetLogPath(string basepath)
        {
            string path = Path.Combine(basepath, "Log");

            lock (typeof(Application))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            return path;
        }


        const string MasterPassPad = "TSviewCloud Master Password";
        private static string _MasterPassword = "";
        public static string MasterPassword
        {
            get { return MasterPassPad + MasterPasswordRaw; }
            set
            {
                MasterPasswordRaw = value;
            }
        }
        public static string MasterPasswordRaw
        {
            get { return _MasterPassword; }
            set
            {
                try
                {
                    if (IsMasterPasswordCorrect || Decrypt(Enc_Check_drive_password, MasterPassPad+value) == Check_pass_password)
                    {
                        _MasterPassword = value;
                        if (string.IsNullOrEmpty(value)) _MasterPassword = "";
                        Enc_Check_drive_password = Encrypt(Check_pass_password, MasterPassword);
                    }
                }
                catch { }
            }
        }
        private const string Check_pass_password = "check for dirve password";
        public static bool IsMasterPasswordCorrect
        {
            get
            {
                try
                {
                    return string.IsNullOrEmpty(_Encrypted_DirvePasswordCheck) || Decrypt(_Encrypted_DirvePasswordCheck, MasterPassword) == Check_pass_password;
                }
                catch
                {
                    return false;
                }
            }
        }
        private static string _Encrypted_DirvePasswordCheck;
        public static string Enc_Check_drive_password
        {
            get
            {
                if (!IsMasterPasswordCorrect)
                    return _Encrypted_DirvePasswordCheck;
                return Encrypt(Check_pass_password, MasterPassword);
            }
            set
            {
                _Encrypted_DirvePasswordCheck = value;
            }
        }




        private static readonly bool IsInstalled = File.Exists(Path.Combine(Application.StartupPath, "_installed.txt"));
        private static readonly string _config_basepath = (IsInstalled) ? GetFileSystemPath(Environment.SpecialFolder.ApplicationData) : "";
        private static string Config_BasePath
        {
            get { return _config_basepath; }
        }
        private static readonly string _filepath = Path.Combine(GetConfigPath(_config_basepath), Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".xml");
        private static string filepath
        {
            get { return _filepath; }
        }
        private static string _ApplicationID;
        public static string ApplicationID
        {
            get { return _ApplicationID; }
        }

        public static string CachePath
        {
            get { return Path.Combine(Config_BasePath, "Servers"); }
        }
        public static string LogPath
        {
            get { return GetLogPath(Config_BasePath); }
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static byte[] _salt = Encoding.ASCII.GetBytes("TSviewCloud config crypt");

        public static string Encrypt(string plaintxt, string password)
        {
            RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.
            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(password, _salt);

                // Create a RijndaelManaged object
                aesAlg = new RijndaelManaged();
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plaintxt);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                aesAlg?.Clear();
            }
        }

        public static string Decrypt(string crypttxt, string password)
        {
            // Declare the RijndaelManaged object
            // used to decrypt the data.
            RijndaelManaged aesAlg = null;

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(password, _salt);

                // Create the streams used for decryption.                
                byte[] bytes = Convert.FromBase64String(crypttxt);
                using (MemoryStream msDecrypt = new MemoryStream(bytes))
                {
                    // Create a RijndaelManaged object
                    // with the specified key and IV.
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    byte[] rawLength = new byte[sizeof(int)];
                    if (msDecrypt.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
                    {
                        throw new SystemException("Stream did not contain properly formatted byte array");
                    }
                    byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
                    if (msDecrypt.Read(buffer, 0, buffer.Length) != buffer.Length)
                    {
                        throw new SystemException("Did not read byte array properly");
                    }
                    aesAlg.IV = buffer;
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            return srDecrypt.ReadToEnd();
                    }
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                aesAlg?.Clear();
            }
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        static Config()
        {
            using (var md5 = new MD5Cng())
            {
                _ApplicationID = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(filepath))).ToLower().Replace("-", "");
            }

            var serializer = new DataContractSerializer(typeof(Savedata));
            try
            {
                using (var xmlr = XmlReader.Create(filepath))
                {
                    var data = (Savedata)serializer.ReadObject(xmlr);

                    Enc_Check_drive_password = data.DrivePasswordCheck;

                    if (data.LogToFile != false)
                        LogToFile = data.LogToFile;
                    if (data.SaveCacheCompressed != true)
                        SaveGZConfig = data.SaveCacheCompressed;
                    if (data.SaveEncrypted != false)
                        SaveEncrypted = data.SaveEncrypted;
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
                    if (data.Main_Size != null)
                        Main_Size = data.Main_Size;
                    if (data.Main_Location != null)
                        Main_Location = data.Main_Location;

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
                    var ccarot = new SavedataCryptCarotDAV
                    {
                        CryptNameHeader = ConfigCarotDAV.CryptNameHeader,
                    };
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
                        LogToFile = LogToFile,
                        SaveCacheCompressed = SaveGZConfig,
                        SaveEncrypted = SaveEncrypted,
                        UploadBandwidthLimit = UploadLimit,
                        DownloadBandwidthLimit = DownloadLimit,
                        ParallelDownload = ParallelDownload,
                        ParallelUpload = ParallelUpload,
                        DownloadBufferSize = DownloadBufferSize,
                        UploadBufferSize = UploadBufferSize,
                        FFplayer = ffdata,
                        CryptCarotDAV = ccarot,
                        DrivePasswordCheck = Enc_Check_drive_password,
                        Main_Size = TSviewCloud.Program.MainForm?.Size ?? Main_Size,
                        Main_Location = TSviewCloud.Program.MainForm?.Location ?? Main_Location,
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
        public bool LogToFile;
        [DataMember]
        public bool SaveCacheCompressed;
        [DataMember]
        public bool SaveEncrypted;
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
        public System.Drawing.Size? Main_Size;
        [DataMember]
        public System.Drawing.Point? Main_Location;

        [DataMember]
        public string DrivePasswordCheck;

        [DataMember]
        public SavedataFFplayer FFplayer;

        [DataMember]
        public SavedataCryptCarotDAV CryptCarotDAV;
    }
}