using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Threading;

namespace TSviewCloudPlugin
{
    public class RemoteServerErrorException : Exception
    {
        public RemoteServerErrorException()
        {
        }

        public RemoteServerErrorException(string message) : base(message)
        {
        }

        public RemoteServerErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public enum ReloadType
    {
        Cache,
        Reload
    };

    [DataContract]
    public enum RemoteItemType
    {
        File,
        Folder,
    };

    public interface IRemoteItem
    {
        bool IsRoot { get; }
        string Server { get; }
        string ID { get; }
        string Path { get; }
        string FullPath { get; }

        string Name { get; }
        long? Size { get; }
        DateTime? ModifiedDate { get; }
        DateTime? CreatedDate { get; }
        string Hash { get; }

        IRemoteItem[] Parents { get; }
        ConcurrentDictionary<string, IRemoteItem> Children { get; }
        RemoteItemType ItemType { get; }

        void FixChain(IRemoteServer server);
        void ChangeParent(IRemoteItem oldp, IRemoteItem newp);

        Job<Stream> DownloadItem(bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> UploadFile(string filename, string uploadname = null, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> MakeFolder(string foldername, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> DeleteItem(bool WeekDepend = false, params Job[] prevJob); // returns parent item
    }

    public interface IRemoteServer
    {
        string ServiceName { get; }
        string Name { get; set; }
        Icon Icon { get; }
        bool IsReady { get; }

        void Init();
        bool Add();

        IRemoteItem this[string path] { get; }
        IRemoteItem PeakItem(string path);

        Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob);
        Job<Stream> DownloadFile(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> DeleteItem(IRemoteItem delteTarget, bool WeekDepend = false, params Job[] prevJob); // returns parent item
    }


    [DataContract]
    public abstract class RemoteItemBase : IRemoteItem
    {
        protected IRemoteServer _server;
        protected IRemoteItem[] _parents;
        protected ConcurrentDictionary<string, IRemoteItem> _children;


        [DataMember(Name = "IsRoot")]
        protected bool isRoot;
        [DataMember(Name = "ServerName")]
        protected string serverName;
        [DataMember(Name = "ItemType")]
        protected RemoteItemType itemtype;
        [DataMember(Name = "Size")]
        protected long? size;
        [DataMember(Name = "ModifiedDate")]
        protected DateTime? modifiedDate;
        [DataMember(Name = "CreatedDate")]
        protected DateTime? createdDate;
        [DataMember(Name = "Hash")]
        protected string hash;

        [DataMember(Name = "Parents")]
        protected string[] parentIDs;
        [DataMember(Name = "Children")]
        protected string[] ChildrenIDs;


        public RemoteItemBase()
        {
            _children = new ConcurrentDictionary<string, IRemoteItem>();
        }

        public RemoteItemBase(IRemoteServer server, params IRemoteItem[] parents): this()
        {
            _parents = (parents?.Length > 0)? parents : new IRemoteItem[] { this };
            _server = server;
            serverName = Server;
            parentIDs = _parents.Select(x => x.ID).ToArray();
        }

        public abstract string ID { get; }
        public abstract string Path { get; }
        public abstract string Name { get; }
        public bool IsRoot => isRoot;
        public IRemoteItem[] Parents => _parents;
        public ConcurrentDictionary<string, IRemoteItem> Children => _children;
        public RemoteItemType ItemType => itemtype;

        public string Server => _server.Name;

        public long? Size => size;
        public DateTime? ModifiedDate => modifiedDate;
        public DateTime? CreatedDate => createdDate;
        public string Hash => hash;
        public string FullPath => Server + "://" + Uri.UnescapeDataString(Path);

        public abstract void FixChain(IRemoteServer server);
        public virtual void ChangeParent(IRemoteItem oldp, IRemoteItem newp)
        {
            _parents = _parents.Where(x => x != oldp).Concat(new[] { newp }).ToArray();
        }

        public virtual Job<Stream> DownloadItem(bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.DownloadFile(this, WeekDepend, prevJob);
        }

        public virtual Job<IRemoteItem> UploadFile(string filename, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            return _server.UploadFile(filename, this, uploadname, WeekDepend, parentJob);
        }

        public Job<IRemoteItem> MakeFolder(string foldername, bool WeekDepend = false, params Job[] parentJob)
        {
            return _server.MakeFolder(foldername, this, WeekDepend, parentJob);
        }

        public Job<IRemoteItem> DeleteItem(bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.DeleteItem(this, WeekDepend, prevJob);
        }
    }

    [DataContract]
    public abstract class RemoteServerBase : IRemoteServer
    {
        [DataMember(Name ="Name")]
        protected string base_name;

        protected bool _IsReady;

        public string Name { get => base_name; set => base_name = value; }
        public string ServiceName => GetServiceName();
        public Icon Icon => GetIcon();

        public bool IsReady => _IsReady;

        public IRemoteItem this[string path] {
            get {
                EnsureItem(path, 1);
                return PeakItem(path);
            }
        }
        public abstract IRemoteItem PeakItem(string path);
        protected abstract void EnsureItem(string path, int depth = 0);

        public abstract void Init();
        public abstract bool Add();
        public abstract string GetServiceName();
        public abstract Icon GetIcon();

        protected virtual void SetUpdate(IRemoteItem target)
        {
            ItemControl.ReloadRequest.AddOrUpdate(target.FullPath, 1, (k, v) => v + 1);
        }

        public abstract Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob);
        public abstract Job<Stream> DownloadFile(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob);
        public abstract Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob);
        public abstract Job<IRemoteItem> DeleteItem(IRemoteItem delteTarget, bool WeekDepend = false, params Job[] prevJob);
    }

    public class RemoteServerFactory
    {
        static RemoteServerFactory()
        {
            Load();
        }

        static public ConcurrentDictionary<string, Type> DllList = new ConcurrentDictionary<string, Type>();
        static public ConcurrentDictionary<string, IRemoteServer> ServerList = new ConcurrentDictionary<string, IRemoteServer>();

        static public string ServerFixedName(string class_name)
        {
            return FixServerName(class_name);
        }

        static public void Register(string name, Type T)
        {
            DllList.AddOrUpdate(name, (k) => T, (k, v) => T);
        }

        static public IRemoteServer Get(string class_name, string base_name)
        {
            try
            {
                var n = Activator.CreateInstance(DllList[class_name]) as IRemoteServer;
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(base_name))
                    {
                        base_name = FixServerName(base_name);
                        ServerList.AddOrUpdate(base_name, (k) => n, (k, v) => n);
                    }
                    n.Name = base_name;
                }
                return n;
            }
            catch
            {
                return null;
            }
        }

        static public void Delete(IRemoteServer target)
        {
            IRemoteServer o;
            while (!ServerList.TryRemove(target.Name, out o)) ;
        }

        static public void Delete(string target)
        {
            IRemoteServer o;
            while (!ServerList.TryRemove(target, out o)) ;
        }

        static public void Load()
        {
            try
            {
                foreach (var file in Directory.GetFiles("plugin", "*.dll"))
                {
                    var dll = Assembly.LoadFrom(file);
                    foreach (var t in dll.GetExportedTypes())
                    {
                        if (t.IsInterface) continue;
                        if (t.IsAbstract) continue;

                        if (!t.GetInterfaces().Contains(typeof(IRemoteServer))) continue;
                        (Activator.CreateInstance(t) as IRemoteServer)?.Init();
                    }
                }
            }
            catch { }
        }

        static private string FixServerName(string orgname)
        {
            while (ServerList.ContainsKey(orgname))
            {
                var m = Regex.Match(orgname, @"^(?<base>.*)_(?<num>\d*)$");
                if (m.Success)
                    orgname = m.Groups["base"].Value + "_" + (int.Parse(m.Groups["num"].Value) + 1).ToString();
                else
                    orgname += "_1";
            }
            return orgname;
        }

        static string cachedir = Path.Combine(TSviewCloudConfig.Config.Config_BasePath, "Cache");

        static public void Save()
        {
            if (Directory.Exists(cachedir))
            {
                Directory.Delete(cachedir, true);
            }
            Directory.CreateDirectory(cachedir);

            foreach (var s in ServerList)
            {
                var dir = cachedir + "\\" + s.Value.ServiceName;
                Directory.CreateDirectory(dir);
                var serializer = new DataContractJsonSerializer(s.Value.GetType());
                if (TSviewCloudConfig.Config.SaveGZConfig)
                {
                    using (var f = new FileStream(dir + "\\" + s.Key + ".xml.gz", FileMode.Create))
                    using (var cf = new GZipStream(f, CompressionMode.Compress))
                    {
                        serializer.WriteObject(cf, s.Value);
                    }
                }
                else
                {
                    using (var f = new FileStream(dir + "\\" + s.Key + ".xml", FileMode.Create))
                    {
                        serializer.WriteObject(f, s.Value);
                    }
                }
            }
        }

        static public void Restore()
        {
            if (!Directory.Exists(cachedir)) return;
            foreach (var s in Directory.GetDirectories(cachedir))
            {
                var service = s.Substring(cachedir.Length + 1);
                foreach (var c in Directory.GetFiles(s, "*.xml"))
                {
                    var connection = Path.GetFileNameWithoutExtension(c);

                    var serializer = new DataContractJsonSerializer(DllList[service]);
                    using (var f = new FileStream(c, FileMode.Open))
                    {
                        var obj = serializer.ReadObject(f) as IRemoteServer;
                        ServerList.AddOrUpdate(connection, (k) => obj, (k, v) => obj);
                    }
                }
                foreach (var c in Directory.GetFiles(s, "*.xml.gz"))
                {
                    var connection = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(c));

                    var serializer = new DataContractJsonSerializer(DllList[service]);
                    using (var f = new FileStream(c, FileMode.Open))
                    using (var cf = new GZipStream(f, CompressionMode.Decompress))
                    {
                        var obj = serializer.ReadObject(cf) as IRemoteServer;
                        ServerList.AddOrUpdate(connection, (k) => obj, (k, v) => obj);
                    }
                }
            }
        }

        static public bool PathIsReady(string url)
        {
            var m = Regex.Match(url, @"^(?<server>[^:]+)(://)(?<path>.*)$");
            if (m.Success)
            {
                return ServerList[m.Groups["server"].Value].IsReady;
            }
            return false;
        }

        static public IRemoteItem PathToItem(string url, ReloadType reload = ReloadType.Cache)
        {
            var m = Regex.Match(url, @"^(?<server>[^:]+)(://)(?<path>.*)$");

            if (!m.Success) return null;

            try
            {
                var server = ServerList[m.Groups["server"].Value];

                IRemoteItem current;
                if (ItemControl.ReloadRequest.TryRemove(server + "://", out int tmp))
                {
                    current = server[""];
                }
                else
                {
                    current = (reload == ReloadType.Cache) ? server.PeakItem("") : server[""];
                }

                var fullpath = m.Groups["path"].Value;
                while (!string.IsNullOrEmpty(fullpath))
                {
                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]+)(/|\\)?(?<next>.*)$");
                    var child = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).FirstOrDefault();
                    if((child == null || child.Children.Count == 0) && reload == ReloadType.Cache)
                    {
                        if (child == null)
                        {
                            current = server[current.Path];
                            current = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).First();
                        }
                        else {
                            current = server[child.Path];
                        }
                    }
                    else
                    {
                        current = child;
                    }
                    if(reload == ReloadType.Reload || ItemControl.ReloadRequest.TryRemove(current.FullPath, out int tmp2))
                        current = server[current.Path];
                    fullpath = m.Groups["next"].Value;
                }
                return current;
            }
            catch
            {
                return null;
            }
        }

        static public Job<IRemoteItem> PathToItemJob(string url, ReloadType reload = ReloadType.Cache)
        {
            var LoadJob = JobControler.CreateNewJob<IRemoteItem>(JobClass.LoadItem);
            LoadJob.DisplayName = "Loading  " + url;
            JobControler.Run(LoadJob, (j) =>
            {
                LoadJob.Progress = -1;
                LoadJob.ProgressStr = "Loading...";
                //LoadJob.ForceHidden = true;

                LoadJob.Result = PathToItem(url, reload);

                LoadJob.Progress = 1;
                LoadJob.ProgressStr = "Done.";
            });
            return LoadJob;
        }

        static public Job<IRemoteItem> PathToItemJob(string baseurl, string relativeurl, ReloadType reload = ReloadType.Cache)
        {
            var LoadJob = JobControler.CreateNewJob<IRemoteItem>(JobClass.LoadItem);
            LoadJob.DisplayName = "Loading  " + relativeurl;
            JobControler.Run(LoadJob, (j) =>
            {
                LoadJob.Progress = -1;
                LoadJob.ProgressStr = "Loading...";
                //LoadJob.ForceHidden = true;

                LoadJob.Result = PathToItem(baseurl, relativeurl, reload);

                LoadJob.Progress = 1;
                LoadJob.ProgressStr = "Done.";
            });
            return LoadJob;
        }

        static public IRemoteItem PathToItem(string baseurl, string relativeurl, ReloadType reload = ReloadType.Cache)
        {
            var current = PathToItem(baseurl, reload);
            if (current == null) return null;

            try
            {
                var fullpath = relativeurl;
                var server = ServerList[current.Server];
                var m = Regex.Match(fullpath, @"^/(?<current>.*)$");
                if (m.Success)
                {

                    if (ItemControl.ReloadRequest.TryRemove(server + "://", out int tmp))
                    {
                        current = server[""];
                    }
                    else
                    {
                        current = (reload == ReloadType.Cache) ? server.PeakItem("") : server[""];
                    }
                    fullpath = m.Groups["current"].Value;
                }

                while (!string.IsNullOrEmpty(fullpath))
                {

                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]+)(/|\\)?(?<next>.*)$");
                    fullpath = m.Groups["next"].Value;
                    if (m.Groups["current"].Value == ".") continue;
                    if (m.Groups["current"].Value == "..")
                    {
                        current = current.Parents[0];
                    }
                    else
                    {
                        var child = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).FirstOrDefault();
                        if ((child == null || child.Children.Count == 0) && reload == ReloadType.Cache)
                        {
                            if (child == null)
                            {
                                current = server[current.Path];
                                current = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).First();
                            }
                            else
                            {
                                current = server[child.Path];
                            }
                        }
                        else
                        {
                            current = child;
                        }
                        if (reload == ReloadType.Reload || ItemControl.ReloadRequest.TryRemove(current.FullPath, out int tmp2))
                            current = server[current.Path];
                    }
                }
                return current;
            }
            catch
            {
                return null;
            }
        }

        static public IRemoteItem[] PathToItemChain(string url, ReloadType reload = ReloadType.Cache)
        {
            var m = Regex.Match(url, @"^(?<server>[^:]+)(://)(?<path>.*)$");

            if (!m.Success) return null;

            var ret = new List<IRemoteItem>();
            try
            {
                var server = ServerList[m.Groups["server"].Value];
                var current = (reload == ReloadType.Cache) ? server.PeakItem("") : server[""];
                var fullpath = m.Groups["path"].Value;
                ret.Add(current);
                while (!string.IsNullOrEmpty(fullpath))
                {

                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]+)(/|\\)?(?<next>.*)$");
                    var child = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).FirstOrDefault();
                    if ((child == null || child.Children.Count == 0) && reload == ReloadType.Cache)
                    {
                        if (child == null)
                        {
                            current = server[current.Path];
                            current = current.Children.Values.Where(x => x.Name == m.Groups["current"].Value).First();
                        }
                        else
                        {
                            current = server[child.Path];
                        }
                    }
                    else
                    {
                        current = child;
                    }
                    if (reload == ReloadType.Reload)
                        current = server[current.Path];
                    ret.Add(current);
                    fullpath = m.Groups["next"].Value;
                }
                return ret.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
