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
using System.Security.Cryptography;

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

    public interface IRemoteItemAttrib
    {
        DateTime? ModifiedDate { get; }
        DateTime? CreatedDate { get; }
    }

    public class RemoteItemAttrib : IRemoteItemAttrib
    {
        private DateTime? _modifiedDate;
        private DateTime? _createdDate;

        public RemoteItemAttrib()
        {
        }

        public RemoteItemAttrib(DateTime? modifiedDate, DateTime? createdDate)
        {
            _modifiedDate = modifiedDate ?? throw new ArgumentNullException(nameof(modifiedDate));
            _createdDate = createdDate ?? throw new ArgumentNullException(nameof(createdDate));
        }

        public DateTime? ModifiedDate => _modifiedDate;
        public DateTime? CreatedDate => _createdDate;
    }

    public interface IRemoteItem
    {
        bool IsRoot { get; }
        string Server { get; }
        string ID { get; }
        string Path { get; }
        string FullPath { get; }

        bool IsReadyRead { get; }
        bool IsBroken { get; set; }

        string Name { get; }
        string PathItemName { get; }
        long? Size { get; }
        DateTime? ModifiedDate { get; set; }
        DateTime? CreatedDate { get; }
        DateTime? AccessDate { get; }
        string Hash { get; }

        DateTime Age { get; }

        IEnumerable<IRemoteItem> Parents { get; }
        IEnumerable<IRemoteItem> Children { get; }
        RemoteItemType ItemType { get; }

        void FixChain(IRemoteServer server);
        void SetParents(IEnumerable<IRemoteItem> newparents);
        void SetParent(IRemoteItem newparent);
        void SetChildren(IEnumerable<IRemoteItem> newchildren);

        Job<Stream> DownloadItemRawJob(long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob);
        Job<Stream> DownloadItemJob(bool WeekDepend = false, params Job[] prevJob);
        Stream DownloadItem(bool WeekDepend = false, params Job[] prevJob);
        Stream DownloadItemRaw(bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> UploadFile(string filename, string uploadname = null, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> UploadStream(Stream source, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> MakeFolder(string foldername, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> DeleteItem(bool WeekDepend = false, params Job[] prevJob); // returns parent item
        Job<IRemoteItem> MoveItem(IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> RenameItem(string newName, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> ChangeAttribItem(IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob);
    }

    public interface IRemoteServer
    {
        string DependsOnService { get; }
        string ServiceName { get; }
        string Name { get; set; }
        Icon Icon { get; }
        bool IsReady { get; }

        void Init();
        bool Add();
        void ClearCache();

        IRemoteItem this[string ID] { get; }
        IRemoteItem PeakItem(string ID);
        IRemoteItem ReloadItem(string ID);

        Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob);
        Job<IRemoteItem> UploadStream(Stream source, IRemoteItem remoteTarget, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob);
        Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget, long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob);
        Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> DeleteItem(IRemoteItem deleteTarget, bool WeekDepend = false, params Job[] prevJob); // returns parent item
        Job<IRemoteItem> MoveItem(IRemoteItem moveItem, IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> RenameItem(IRemoteItem targetItem, string newName, bool WeekDepend = false, params Job[] prevJob);
        Job<IRemoteItem> ChangeAttribItem(IRemoteItem targetItem, IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob);
    }


    [DataContract]
    public abstract class RemoteItemBase : IRemoteItem
    {
        protected IRemoteServer _server;


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
        [DataMember(Name = "AccessDate")]
        protected DateTime? accessDate;
        [DataMember(Name = "Hash")]
        protected string hash;

        [DataMember(Name = "Parents")]
        protected string[] parentIDs;
        [DataMember(Name = "Children")]
        protected string[] ChildrenIDs;

        private DateTime age;
        private bool _isBroken;

        public RemoteItemBase()
        {
        }

        public RemoteItemBase(IRemoteServer server, params IRemoteItem[] parents) : this()
        {
            SetParents(parents);
            _server = server;
            serverName = Server;
            Age = DateTime.Now;
        }
 

        public abstract string ID { get; }
        public abstract string Path { get; }
        public abstract string Name { get; }
        public bool IsRoot => isRoot;
        public virtual IEnumerable<IRemoteItem> Parents => parentIDs?.Where(x => x != null).Select(x => _server.PeakItem(x))?.Where(x => x != null) ?? new List<IRemoteItem>();
        public virtual IEnumerable<IRemoteItem> Children => ChildrenIDs?.Where(x => x != null).Select(x => _server.PeakItem(x))?.Where(x => x != null) ?? new List<IRemoteItem>();

        public RemoteItemType ItemType => itemtype;

        public string Server => _server.Name;

        public virtual long? Size => size;
        public virtual DateTime? ModifiedDate { get => modifiedDate; set => SetModifiedDate(value); }
        public virtual DateTime? CreatedDate => createdDate;
        public virtual DateTime? AccessDate => accessDate;
        public virtual string Hash => hash;
        public virtual string FullPath => Server + "://" + Path;

        public virtual DateTime Age { get => age; set => age = value; }

        protected virtual void SetModifiedDate(DateTime? newdateTime)
        {
            var job = _server.ChangeAttribItem(this, new RemoteItemAttrib(newdateTime, null));
            job.Wait();
        }

        public virtual bool IsReadyRead => true;

        public bool IsBroken { get => _isBroken; set => _isBroken = value; }

        public virtual string PathItemName => Name;

        public void SetParents(IEnumerable<IRemoteItem> newparents)
        {
            parentIDs = newparents?.Select(x => x?.ID).ToArray();
            Age = DateTime.Now;
        }

        public void SetParent(IRemoteItem newparent)
        {
            SetParents(new[] { newparent });
            Age = DateTime.Now;
        }

        public void SetChildren(IEnumerable<IRemoteItem> newchildren)
        {
            ChildrenIDs = newchildren?.Select(x => x?.ID).ToArray();
            Age = DateTime.Now;
        }


        public virtual void FixChain(IRemoteServer server)
        {
            Age = DateTime.Now;
        }

        public virtual Job<Stream> DownloadItemRawJob(long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            return _server.DownloadItemRaw(this, offset, WeekDepend, hidden, prevJob);
        }

        public virtual Job<IRemoteItem> UploadFile(string filename, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            return _server.UploadFile(filename, this, uploadname, WeekDepend, parentJob);
        }

        public virtual Job<IRemoteItem> UploadStream(Stream source, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob)
        {
            return _server.UploadStream(source, this, uploadname, streamsize, WeekDepend, parentJob);
        }

        public virtual Job<IRemoteItem> MakeFolder(string foldername, bool WeekDepend = false, params Job[] parentJob)
        {
            return _server.MakeFolder(foldername, this, WeekDepend, parentJob);
        }

        public virtual Job<IRemoteItem> DeleteItem(bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.DeleteItem(this, WeekDepend, prevJob);
        }

        public virtual Job<Stream> DownloadItemJob(bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.DownloadItem(this, WeekDepend, prevJob);
        }

        public virtual Stream DownloadItem(bool WeekDepend = false, params Job[] prevJob)
        {
            var job = DownloadItemJob(WeekDepend, prevJob);
            job.Wait();
            return job.Result;
        }

        public virtual Stream DownloadItemRaw(bool WeekDepend = false, params Job[] prevJob)
        {
            var job = DownloadItemRawJob(0, WeekDepend, prevJob: prevJob);
            job.Wait();
            return job.Result;
        }

        public virtual Job<IRemoteItem> MoveItem(IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.MoveItem(this, moveToItem, WeekDepend, prevJob);
        }

        public Job<IRemoteItem> RenameItem(string newName, bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.RenameItem(this, newName, WeekDepend, prevJob);
        }

        public Job<IRemoteItem> ChangeAttribItem(IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob)
        {
            return _server.ChangeAttribItem(this, newAttrib, WeekDepend, prevJob);
        }
    }

    [DataContract]
    public abstract class RemoteServerBase : IRemoteServer
    {
        [DataMember(Name ="Name")]
        protected string base_name;
        [DataMember(Name = "DependsOnService")]
        protected string _dependService;

        protected bool _IsReady;

        public string Name { get => base_name; set => base_name = value; }
        public string ServiceName => GetServiceName();
        public Icon Icon => GetIcon();

        public bool IsReady => _IsReady;

        public string DependsOnService => _dependService;

        protected abstract string RootID { get; }

        public virtual IRemoteItem this[string ID] {
            get {
                try
                {
                    if (ID == null) return null;
                    if (ID == RootID) ID = "";
                    EnsureItem(ID);
                    return PeakItem(ID);
                }
                catch
                {
                    return null;
                }
            }
        }
        public abstract IRemoteItem PeakItem(string ID);
        protected abstract void EnsureItem(string ID, int depth = 0);
        public abstract IRemoteItem ReloadItem(string ID);

        public abstract void Init();
        public abstract bool Add();
        public abstract void ClearCache();

        public abstract string GetServiceName();
        public abstract Icon GetIcon();

        protected virtual void SetUpdate(IRemoteItem target)
        {
            ItemControl.ReloadRequest.AddOrUpdate(target.FullPath, 1, (k, v) => v + 1);
            if(TSviewCloud.MultiInstance.InstanceCount > 1)
            {
                TSviewCloud.MultiInstance.RegisterReload(target.FullPath);
            }
        }

        public abstract Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget, long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob);
        public abstract Job<IRemoteItem> DeleteItem(IRemoteItem delteTarget, bool WeekDepend = false, params Job[] prevJob);
        public abstract Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob);
        public abstract Job<IRemoteItem> RenameItem(IRemoteItem targetItem, string newName, bool WeekDepend = false, params Job[] prevJob);
        public abstract Job<IRemoteItem> ChangeAttribItem(IRemoteItem targetItem, IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob);

        public abstract Job<IRemoteItem> UploadStream(Stream source, IRemoteItem remoteTarget, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob);

        public virtual Job CheckUpload(IRemoteItem remoteTarget, string uploadname, long? streamsize = null, bool WeekDepend = false, params Job[] parentJob)
        {
            var conflicts = remoteTarget.Children?.Where(x => x.Name.ToLower() == uploadname.ToLower());
            if((conflicts?.Count()?? 0) == 0)
            {
                return null;
            }
            if(TSviewCloudConfig.Config.UploadConflictBehavior == TSviewCloudConfig.UploadBehavior.SkipAlways)
            {
                TSviewCloud.FormConflicts.Instance.AddResult(remoteTarget.FullPath + "/" + uploadname, "Skip upload");
                throw new Exception("conflict and always skip");
            }
            if (TSviewCloudConfig.Config.UploadConflictBehavior == TSviewCloudConfig.UploadBehavior.SkipSameSize)
            {
                if (streamsize == null || conflicts.Any(x => x.Size == streamsize))
                {
                    TSviewCloud.FormConflicts.Instance.AddResult(remoteTarget.FullPath + "/" + uploadname, "Skip upload (same size)");
                    throw new Exception("conflict and same size skip");
                }
            }

            var job = JobControler.CreateNewJob(JobClass.ControlMaster, depends: parentJob);
            job.WeekDepend = WeekDepend;
            JobControler.Run(job, (j) =>
            {
                var del = conflicts.Select(x => DeleteItem(x, true, j));

                foreach(var d in del)
                {
                    d.Wait(ct: j.Ct);
                }
            });
            return job;
        }

        public abstract Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob);


        public virtual Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            filename = ItemControl.GetOrgFilename(filename);

            TSviewCloudConfig.Config.Log.LogOut("[UploadFile] " + filename);
            var filesize = new FileInfo(ItemControl.GetLongFilename(filename)).Length;
            var short_filename = Path.GetFileName(ItemControl.GetLongFilename(filename));

            var filestream = new FileStream(ItemControl.GetLongFilename(filename), FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024);
            var job = UploadStream(filestream, remoteTarget, short_filename, filesize, WeekDepend, parentJob);

            return job;
        }


        protected abstract Job<IRemoteItem> MoveItemOnServer(IRemoteItem moveItem, IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob);

        public virtual Job<IRemoteItem> MoveItem(IRemoteItem moveItem, IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;
            TSviewCloudConfig.Config.Log.LogOut("[MoveItem(RemoteServerBase)] " + moveItem.FullPath);

            if (moveToItem.Server == moveItem.Parents?.FirstOrDefault()?.Server)
                return MoveItemOnServer(moveItem, moveToItem, WeekDepend, prevJob);

            if (moveItem.ItemType == RemoteItemType.File)
            {
                try
                {
                    var checkjob = CheckUpload(moveToItem, moveItem.Name, moveItem.Size ?? 0, WeekDepend, prevJob);
                    if (checkjob != null)
                    {
                        WeekDepend = false;
                        prevJob = new[] { checkjob };
                    }
                }
                catch
                {
                    return null;
                }
                var movejob = JobControler.CreateNewJob<IRemoteItem>(
                    type: JobClass.RemoteUpload,
                    info: new JobControler.SubInfo
                    {
                        type = JobControler.SubInfo.SubType.UploadFilePre,
                        size = moveItem.Size ?? 0,
                    },
                    depends: prevJob);
                movejob.WeekDepend = WeekDepend;
                movejob.DisplayName = moveItem.FullPath;
                movejob.ProgressStr = "wait for download";
                JobControler.Run<IRemoteItem>(movejob, (j) =>
                {
                    j.ProgressStr = "move progress...";
                    j.Progress = -1;
                    var downjob = RemoteServerFactory.PathToItem(moveItem.FullPath).DownloadItemRawJob(WeekDepend: true, prevJob: j);
                    downjob.Wait(ct: j.Ct);
                    j.JobInfo.pos = moveItem.Size ?? 0; // hand over size to upload task
                    j.ForceHidden = true;
                    var uploadjob = moveToItem.UploadStream(downjob.Result, moveItem.Name, moveItem.Size ?? 0, WeekDepend, prevJob);
                    uploadjob.Wait(ct: j.Ct);
                    j.Result = uploadjob.Result;
                    j.ProgressStr = "done";
                    j.Progress = 1;
                });
                return movejob;
            }

            var loadjob = RemoteServerFactory.PathToItemJob(moveItem.FullPath);

            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Upload,
                info: new JobControler.SubInfo
                {
                    type = JobControler.SubInfo.SubType.UploadDirectory,
                },
                depends: moveToItem.MakeFolder(moveItem.Name, WeekDepend, prevJob?.Concat(new[] { loadjob }).ToArray()??new[] { loadjob } ));
            job.DisplayName = string.Format("Upload Folder {0} to {1}", moveItem.FullPath, moveToItem.FullPath);
            job.ProgressStr = "wait for upload.";
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var newdir))
                {
                    j.Progress = -1;
                    j.ProgressStr = "upload...";
                    var joblist = new List<Job<IRemoteItem>>();
                    joblist.AddRange(RemoteServerFactory.PathToItem(moveItem.FullPath).Children?.Select(x => x?.MoveItem(newdir, WeekDepend: true, prevJob: j)));
                    //Parallel.ForEach(joblist, (x) => x.Wait(ct: job.Ct));
                    j.Result = newdir;
                }
                j.Progress = 1;
                j.ProgressStr = "done";
            });
            return job;
        }
    }

    public class RemoteServerFactory
    {
        static RemoteServerFactory()
        {
            Load();
        }

        static public ConcurrentDictionary<string, Type> DllList = new ConcurrentDictionary<string, Type>();
        static public ConcurrentDictionary<string, IRemoteServer> ServerList = new ConcurrentDictionary<string, IRemoteServer>();


        static private ConcurrentDictionary<string, (string server, string ID)> itemCache = new ConcurrentDictionary<string, (string server, string ID)>();

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
            while (!ServerList.TryRemove(target.Name, out o))
                if(!ServerList.TryGetValue(target.Name, out o))
                    break;
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
            if (string.IsNullOrEmpty(orgname)) return orgname;

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

        static string cachedir = TSviewCloudConfig.Config.CachePath;
        static byte[] _salt = Encoding.ASCII.GetBytes("TSviewCloud server configuration");

        static public void Save()
        {
            int retry = 5;
            do
            {
                try
                {
                    if (Directory.Exists(cachedir))
                    {
                        Directory.Delete(cachedir, true);
                    }
                    break;
                }
                catch
                {
                    Task.Delay(500).Wait();
                    continue;
                }
            } while (retry-- > 0);
            Directory.CreateDirectory(cachedir);

            Parallel.ForEach(ServerList,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                (s) =>
                {
                    var dir = cachedir + "\\" + s.Value.ServiceName;
                    Directory.CreateDirectory(dir);
                    var serializer = new DataContractJsonSerializer(s.Value.GetType());

                    if (TSviewCloudConfig.Config.SaveEncrypted)
                    {
                        RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.
                        try
                        {
                            // generate the key from the shared secret and the salt
                            Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(TSviewCloudConfig.Config.MasterPassword, _salt);

                            // Create a RijndaelManaged object
                            aesAlg = new RijndaelManaged();
                            aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                            // Create a decryptor to perform the stream transform.
                            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                            using (var f = new FileStream(dir + "\\" + s.Key + ".xml.enc.gz", FileMode.Create))
                            using (var cf = new GZipStream(f, CompressionMode.Compress))
                            {
                                // prepend the IV
                                cf.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                                cf.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                                using (CryptoStream csEncrypt = new CryptoStream(cf, encryptor, CryptoStreamMode.Write))
                                {
                                    serializer.WriteObject(csEncrypt, s.Value);
                                }
                            }
                        }
                        finally
                        {
                            // Clear the RijndaelManaged object.
                            if (aesAlg != null)
                                aesAlg.Clear();
                        }
                    }
                    else
                    {
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
                });
        }

        static public void Restore()
        {
            if (!Directory.Exists(cachedir)) return;
            Parallel.ForEach(Directory.GetDirectories(cachedir),
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                (s) =>
                {
                    var service = s.Substring(cachedir.Length + 1);

                    Parallel.ForEach(Directory.GetFiles(s, "*.xml"),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (c) =>
                        {
                            var connection = Path.GetFileNameWithoutExtension(c);

                            var serializer = new DataContractJsonSerializer(DllList[service]);
                            using (var f = new FileStream(c, FileMode.Open))
                            {
                                var obj = serializer.ReadObject(f) as IRemoteServer;
                                ServerList.AddOrUpdate(connection, (k) => obj, (k, v) => obj);
                            }
                        });
                    Parallel.ForEach(Directory.GetFiles(s, "*.xml.gz"),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (c) =>
                        {
                            var connection = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(c));

                            var serializer = new DataContractJsonSerializer(DllList[service]);
                            using (var f = new FileStream(c, FileMode.Open))
                            using (var cf = new GZipStream(f, CompressionMode.Decompress))
                            {
                                var obj = serializer.ReadObject(cf) as IRemoteServer;
                                ServerList.AddOrUpdate(connection, (k) => obj, (k, v) => obj);
                            }
                        });
                    Parallel.ForEach(Directory.GetFiles(s, "*.xml.enc.gz"),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (c) =>
                        {
                            var connection = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(c)));

                            var serializer = new DataContractJsonSerializer(DllList[service]);
                            using (var f = new FileStream(c, FileMode.Open))
                            using (var cf = new GZipStream(f, CompressionMode.Decompress))
                            {
                                // Declare the RijndaelManaged object
                                // used to decrypt the data.
                                RijndaelManaged aesAlg = null;

                                try
                                {
                                    // generate the key from the shared secret and the salt
                                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(TSviewCloudConfig.Config.MasterPassword, _salt);

                                    // Create a RijndaelManaged object
                                    // with the specified key and IV.
                                    aesAlg = new RijndaelManaged();
                                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                                    // Get the initialization vector from the encrypted stream
                                    byte[] rawLength = new byte[sizeof(int)];
                                    if (cf.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
                                    {
                                        throw new SystemException("Stream did not contain properly formatted byte array");
                                    }
                                    byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
                                    if (cf.Read(buffer, 0, buffer.Length) != buffer.Length)
                                    {
                                        throw new SystemException("Did not read byte array properly");
                                    }
                                    aesAlg.IV = buffer;
                                    // Create a decrytor to perform the stream transform.
                                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                                    using (CryptoStream csDecrypt = new CryptoStream(cf, decryptor, CryptoStreamMode.Read))
                                    {
                                        var obj = serializer.ReadObject(csDecrypt) as IRemoteServer;
                                        ServerList.AddOrUpdate(connection, (k) => obj, (k, v) => obj);

                                    }
                                }
                                finally
                                {
                                    // Clear the RijndaelManaged object.
                                    aesAlg?.Clear();
                                }

                            }
                        });
                });
        }

        static public void ClearCache()
        {
            itemCache.Clear();
            ItemControl.ReloadRequest.Clear();

            Dictionary<string, List<IRemoteServer>> dependlist = new Dictionary<string, List<IRemoteServer>>();

            int servercount = 0;
            foreach (var s in ServerList.Values)
            {
                var depends = s.DependsOnService ?? "";
                if (!dependlist.ContainsKey(depends))
                    dependlist[depends] = new List<IRemoteServer>();
                dependlist[depends].Add(s);
                servercount++;
            }

            Queue<IRemoteServer> donelist = new Queue<IRemoteServer>();
            List<string> deadlist = new List<string>();
            foreach(var first in dependlist[""])
            {
                first.ClearCache();
                donelist.Enqueue(first);
                servercount--;
            }
            while (servercount > 0 && donelist.Count > 0)
            {
                var d = donelist.Dequeue();
                if (dependlist.ContainsKey(d.Name))
                {
                    foreach(var slave in dependlist[d.Name])
                    {
                        if (deadlist.Contains(d.Name))
                        {
                            deadlist.Add(slave.Name);
                            Delete(slave);
                        }
                        else
                        {
                            slave.ClearCache();
                            if (!slave.IsReady)
                            {
                                deadlist.Add(slave.Name);
                                Delete(slave);
                            }
                        }
                        donelist.Enqueue(slave);
                        servercount--;
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
                IRemoteItem current = null;
                if (itemCache.TryGetValue(url, out var v))
                {
                    current = server.PeakItem(v.ID);
                    if (reload == ReloadType.Cache && current != null && !ItemControl.ReloadRequest.TryRemove(current.FullPath, out int tmp2))
                    {
                        itemCache.AddOrUpdate(url, (server.Name, current.ID), (key, val) => (server.Name, current.ID));
                        return current;
                    }
                    else
                    {
                        current = server.ReloadItem(current.ID);
                        if (current != null)
                        {
                            itemCache.AddOrUpdate(url, (server.Name, current.ID), (key, val) => (server.Name, current.ID));
                            return current;
                        }
                    }
                }

                if (ItemControl.ReloadRequest.TryRemove(server + "://", out int tmp))
                {
                    current = server.ReloadItem("");
                }
                else
                {
                    current = (reload == ReloadType.Cache) ? server.PeakItem("") : server.ReloadItem("");
                }

                var fullpath = m.Groups["path"].Value;
                while (!string.IsNullOrEmpty(fullpath))
                {
                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                    if (!string.IsNullOrEmpty(m.Groups["current"].Value))
                    {
                        var child = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).FirstOrDefault();
                        if (child == null || !((child.Children?.Count() > 0) && reload == ReloadType.Cache))
                        {
                            if (child == null)
                            {
                                current = server.ReloadItem(current.ID);
                                current = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).FirstOrDefault();
                                if (current == null)
                                {
                                    itemCache.TryRemove(url, out var tmp1);
                                    return null;
                                }
                            }
                            else
                            {
                                current = server[child.ID];
                            }
                        }
                        else
                        {
                            current = child;
                        }
                        if (fullpath == "" && reload == ReloadType.Reload)
                        {
                            current = server.ReloadItem(current.ID);
                        }
                        else if (ItemControl.ReloadRequest.TryRemove(current.FullPath, out int tmp2))
                        {
                            current = server.ReloadItem(current.ID);
                        }
                    }
                    fullpath = m.Groups["next"].Value;
                }

                if(reload == ReloadType.Reload && current.Children.Count() > 0)
                {
                    foreach(var child in current.Children.ToArray())
                    {
                        server.ReloadItem(child.ID);
                    }
                }

                itemCache.AddOrUpdate(url, (server.Name, current.ID), (key, val) => (server.Name, current.ID));
                return current;
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(url);
                itemCache.TryRemove(url, out var tmp);
                return null;
            }
        }

        static public Job<IRemoteItem> PathToItemJob(string url, ReloadType reload = ReloadType.Cache)
        {
            var LoadJob = JobControler.CreateNewJob<IRemoteItem>(JobClass.LoadItem);
            LoadJob.DisplayName = "Loading  " + url;
            JobControler.Run<IRemoteItem>(LoadJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                //LoadJob.ForceHidden = true;

                j.Result = PathToItem(url, reload);

                j.Progress = 1;
                j.ProgressStr = "Done.";
            });
            return LoadJob;
        }

        static public Job<IRemoteItem> PathToItemJob(string baseurl, string relativeurl, ReloadType reload = ReloadType.Cache)
        {
            var LoadJob = JobControler.CreateNewJob<IRemoteItem>(JobClass.LoadItem);
            LoadJob.DisplayName = "Loading  " + relativeurl;
            JobControler.Run<IRemoteItem>(LoadJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                //j.ForceHidden = true;

                j.Result = PathToItem(baseurl, relativeurl, reload);

                j.Progress = 1;
                j.ProgressStr = "Done.";
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

                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                    fullpath = m.Groups["next"].Value;
                    if (string.IsNullOrEmpty(m.Groups["current"].Value)) continue;
                    if (m.Groups["current"].Value == ".") continue;
                    if (m.Groups["current"].Value == "..")
                    {
                        current = current.Parents?.FirstOrDefault() ?? server.PeakItem("");
                    }
                    else
                    {
                        var child = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).FirstOrDefault();
                        if (!(child?.Children?.Count() > 0) && reload == ReloadType.Cache)
                        {
                            if (child == null)
                            {
                                current = server.ReloadItem(current.ID);
                                current = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).First();
                            }
                            else
                            {
                                current = server[child.ID];
                            }
                        }
                        else
                        {
                            current = child;
                        }
                        if (reload == ReloadType.Reload || ItemControl.ReloadRequest.TryRemove(current.FullPath, out int tmp3))
                            current = server.ReloadItem(current.ID);
                    }
                }
                if (reload == ReloadType.Reload && current.Children.Count() > 0)
                {
                    foreach (var child in current.Children.ToArray())
                    {
                        server.ReloadItem(child.ID);
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

                    m = Regex.Match(fullpath, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                    if (!string.IsNullOrEmpty(m.Groups["current"].Value))
                    {
                        var child = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).FirstOrDefault();
                        if (!(child?.Children?.Count() > 0) && reload == ReloadType.Cache)
                        {
                            if (child == null)
                            {
                                current = server.ReloadItem(current.ID);
                                current = current.Children.Where(x => x.PathItemName == m.Groups["current"].Value).First();
                            }
                            else
                            {
                                current = server[child.ID];
                            }
                        }
                        else
                        {
                            current = child;
                        }
                        if (reload == ReloadType.Reload)
                            current = server.ReloadItem(current.ID);
                        ret.Add(current);
                    }
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
