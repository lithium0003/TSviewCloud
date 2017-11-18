using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Soap;
using System.Runtime.Serialization.Json;
using System.Collections.Concurrent;
using System.Threading;
using LibCryptCarotDAV;
using System.Text.RegularExpressions;

namespace TSviewCloudPlugin
{
    [DataContract]
    public class CarotCryptSystemItem : RemoteItemBase
    {
        [DataMember(Name = "ID")]
        private string orgpath;
 
        internal virtual IRemoteItem orgItem
        {
            get
            {
                return RemoteServerFactory.PathToItem(orgpath);
            }
        }

        private string decryptedName;
        private string decryptedPath;

        public override long? Size
        {
            get
            {
                return size = orgItem?.Size - (CryptCarotDAV.BlockSizeByte + CryptCarotDAV.CryptFooterByte + CryptCarotDAV.CryptFooterByte);
            }
        }
        public override DateTime? ModifiedDate
        {
            get
            {
                return modifiedDate = orgItem?.ModifiedDate;
            }
        }
        public override DateTime? CreatedDate
        {
            get
            {
                return createdDate = orgItem?.CreatedDate;
            }
        }

        public CarotCryptSystemItem() : base()
        {

        }

        public CarotCryptSystemItem(IRemoteServer server, IRemoteItem orgItem, params IRemoteItem[] parent) : base(server, parent)
        {
            if (!(parent?.Length > 0)) isRoot = true;

            orgpath = orgItem.FullPath;
            itemtype = orgItem.ItemType;
            size = orgItem?.Size - (CryptCarotDAV.BlockSizeByte + CryptCarotDAV.CryptFooterByte + CryptCarotDAV.CryptFooterByte);
            modifiedDate = orgItem.ModifiedDate;
            createdDate = orgItem.CreatedDate;

            decryptedName = (_server as CarotCryptSystem).CryptCarot.DecryptFilename(orgItem.Name) ?? "";
            decryptedPath = OrgPathToPath(orgpath);

            if (isRoot) SetParent(this);
        }

        public override string ID => orgpath;

        private string OrgPathToPath(string path)
        {
            if (string.IsNullOrEmpty(path) || (_server as CarotCryptSystem).cryptRootPath == path)
                return "";
                
            if (!path.StartsWith((_server as CarotCryptSystem).cryptRootPath)) throw new Exception("internal error: CarotCryptSystemItem rootpath");

            var ret = new List<string>();
            path = path.Substring((_server as CarotCryptSystem).cryptRootPath.Length);

            while (!string.IsNullOrEmpty(path)) {
                var m = Regex.Match(path, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                path = m.Groups["next"].Value;
                if (string.IsNullOrEmpty(m.Groups["current"].Value)) continue;
                if (m.Groups["current"].Value == ".") continue;
                if (m.Groups["current"].Value == "..")
                {
                    if (ret.Count > 0)
                        ret.RemoveAt(ret.Count - 1);
                }
                else
                {
                    ret.Add((_server as CarotCryptSystem).CryptCarot.DecryptFilename(m.Groups["current"].Value));
                }
            }
            return string.Join("/", ret);
        }

        public override string Path => decryptedPath;

        public override string Name => decryptedName;

        public override void FixChain(IRemoteServer server)
        {
            try
            {
                _server = server;
                var orgItem = RemoteServerFactory.PathToItem(orgpath);
                if (orgItem == null)
                {
                    (_server as CarotCryptSystem)?.RemoveItem(ID);
                    return;
                }
                decryptedPath = OrgPathToPath(orgpath);
                decryptedName = (_server as CarotCryptSystem).CryptCarot.DecryptFilename(orgItem.Name) ?? "";
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(ID);
            }
            base.FixChain(server);
        }

    }

    [DataContract]
    public class CarotCryptSystem : RemoteServerBase
    {
        [DataMember(Name = "CryptNameHeader")]
        private string cryptNameHeader;
        [DataMember(Name = "CryptRootPath")]
        internal string cryptRootPath;
        [DataMember(Name = "Password")]
        public string _DrivePassword;

        private ConcurrentDictionary<string, CarotCryptSystemItem> pathlist;

        private ConcurrentDictionary<string, ManualResetEventSlim> loadinglist;

        const string hidden_pass = "CarotDAV Drive Password";
        public string DrivePassword
        {
            get
            {
                return TSviewCloudConfig.Config.Decrypt(_DrivePassword, hidden_pass);
            }
            set
            {
                _DrivePassword = TSviewCloudConfig.Config.Encrypt(_DrivePassword, hidden_pass);
            }
        }

        internal CryptCarotDAV CryptCarot;

        public CarotCryptSystem()
        {
            pathlist = new ConcurrentDictionary<string, CarotCryptSystemItem>();
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
        }

        public override bool Add()
        {
            var picker = new TSviewCloud.FormTreeSelect
            {
                Text = "Select encrypt root folder"
            };

            if (picker.ShowDialog() != DialogResult.OK) return false;
            if (picker.SelectedItem == null) return false;
            if (picker.SelectedItem.ItemType == RemoteItemType.File) return false;

            var pass = new FormInputPass();

            if (pass.ShowDialog() != DialogResult.OK) return false;
            CryptCarot = new CryptCarotDAV(pass.CryptNameHeader)
            {
                Password = pass.Password
            };
            DrivePassword = pass.Password;
            cryptNameHeader = pass.CryptNameHeader;

            cryptRootPath = picker.SelectedItem.FullPath;
            _dependService = picker.SelectedItem.Server;
            var root = new CarotCryptSystemItem(this, picker.SelectedItem, null);
            pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            EnsureItem("", 1);

            _IsReady = true;
            TSviewCloudConfig.Config.Log.LogOut("[Add] CarotCryptSystem {0} as {1}", cryptRootPath, Name);
            return true;
        }

        public override void ClearCache()
        {
            _IsReady = false;
            RemoteServerFactory.ServerList[_dependService].ClearCache();
            pathlist.Clear();
            var root = new CarotCryptSystemItem(this, RemoteServerFactory.PathToItem(cryptRootPath), null);
            pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            EnsureItem("", 1);
            _IsReady = true;
        }


        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] LocalSystem {0} as {1}", cryptRootPath, Name);
            CryptCarot = new CryptCarotDAV(cryptNameHeader)
            {
                Password = DrivePassword
            };
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "CryptCarotDAV";
            job.ProgressStr = "waiting parent";

            JobControler.Run(job, (j) =>
            {
                j.ProgressStr = "Loading...";
                j.Progress = -1;

                try
                {
                    int waitcount = 500;
                    while (!(RemoteServerFactory.ServerList.Keys.Contains(_dependService) && RemoteServerFactory.ServerList[_dependService].IsReady))
                    {
                        Task.Delay(50).Wait(job.Ct);
                        if (waitcount-- == 0) throw new FileNotFoundException("Depend Service is not ready.", _dependService);
                    }
                }
                catch
                {
                    RemoteServerFactory.Delete(this);
                    return;
                }

                if (pathlist == null)
                {
                    pathlist = new ConcurrentDictionary<string, CarotCryptSystemItem>();
                    var root = new CarotCryptSystemItem(this, RemoteServerFactory.PathToItem(cryptRootPath), null);
                    pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                    EnsureItem("", 1);
                }
                else
                {
                    Parallel.ForEach(pathlist.Values.ToArray(), (x) => x.FixChain(this));
                }

                j.ProgressStr = "Done";
                j.Progress = 1;

                _IsReady = true;
            });
        }

        protected override string RootID => cryptRootPath;

        public override IRemoteItem PeakItem(string ID)
        {
            if (ID == RootID) ID = "";
            try
            {
                return pathlist[ID];
            }
            catch
            {
                return null;
            }
        }
        protected override void EnsureItem(string ID, int depth = 0)
        {
            if (ID == RootID) ID = "";
            try
            {
                TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(CarotCryptSystem)] " + ID);
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    LoadItems(ID, depth);
                item = pathlist[ID];
            }
            catch
            {
                LoadItems(ID, depth);
            }
        }
        public override IRemoteItem ReloadItem(string ID)
        {
            if (ID == RootID) ID = "";
            try
            {
                TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(CarotCryptSystem)] " + ID);
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    LoadItems(ID, 1, true);
                item = pathlist[ID];
            }
            catch
            {
                LoadItems(ID, 1, true);
            }
            return PeakItem(ID);
        }

        private string pathToCryptedpath(string path)
        {
            var ret = new List<string>();

            while (!string.IsNullOrEmpty(path))
            {
                var m = Regex.Match(path, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                path = m.Groups["next"].Value;
                if (string.IsNullOrEmpty(m.Groups["current"].Value)) continue;
                if (m.Groups["current"].Value == ".") continue;
                if (m.Groups["current"].Value == "..")
                {
                    if (ret.Count > 0)
                        ret.RemoveAt(ret.Count - 1);
                }
                else
                {
                    ret.Add(CryptCarot.EncryptFilename(m.Groups["current"].Value));
                }
            }
            return string.Join("/", ret);
        }

        private void LoadItems(string ID, int depth = 0, bool deep = false)
        {
            if (depth < 0) return;
            ID = ID ?? "";

            bool master = true;
            loadinglist.AddOrUpdate(ID, new ManualResetEventSlim(false), (k, v) =>
            {
                if (v == null) return new ManualResetEventSlim(false);
                master = false;
                return v;
            });

            if (!master)
            {
                while (loadinglist.TryGetValue(ID, out var tmp) && tmp != null)
                {
                    tmp.Wait();
                }
                return;
            }

            try
            {
                TSviewCloudConfig.Config.Log.LogOut("[LoadItems(CarotCryptSystem)] " + ID);

                var orgID = (string.IsNullOrEmpty(ID)) ? cryptRootPath : ID;
                if (!orgID.StartsWith(cryptRootPath))
                    throw new ArgumentException("ID is not in root path", "ID");
                var orgitem = RemoteServerFactory.PathToItem(orgID, (deep)? ReloadType.Reload: ReloadType.Cache);
                if (orgitem?.Children != null && orgitem.Children?.Count() != 0)
                {
                    var ret = new List<CarotCryptSystemItem>();
                    Parallel.ForEach(
                        orgitem.Children,
                        () => new List<CarotCryptSystemItem>(),
                        (x, state, local) =>
                        {
                            if (!x.Name.StartsWith(cryptNameHeader)) return local;

                            var child = RemoteServerFactory.PathToItem(x.FullPath, (deep) ? ReloadType.Reload : ReloadType.Cache);
                            if (child == null)
                                return local;

                            var item = new CarotCryptSystemItem(this, child, pathlist[ID]);
                            pathlist.AddOrUpdate(item.ID, (k) => item, (k, v) => item);
                            local.Add(item);
                            return local;
                        },
                         (result) =>
                         {
                             lock (ret)
                                 ret.AddRange(result);
                         }
                    );
                    pathlist[ID].SetChildren(ret);
                    if (depth > 0)
                        Parallel.ForEach(pathlist[ID].Children, (x) => { LoadItems(x.ID, depth - 1); });

                }
                else
                {
                    pathlist[ID].SetChildren(null);
                }
            }
            finally
            {
                Task.Delay(10).ContinueWith((t) =>
                {
                    ManualResetEventSlim tmp2;
                    while (!loadinglist.TryRemove(ID, out tmp2))
                        Thread.Sleep(10);
                    tmp2.Set();
                });
            }
        }

        public override Icon GetIcon()
        {
            return LibCryptCarotDAV.Properties.Resources.carot;
        }

        public override string GetServiceName()
        {
            return "CarotCrypt";
        }

        public override void Init()
        {
            RemoteServerFactory.Register(GetServiceName(), typeof(CarotCryptSystem));
        }

        public override Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[MakeFolder(CarotCryptSystem)] " + foldername);
 
            var parent = pathlist[remoteTarget.ID];
            var orgmakejob = parent.orgItem.MakeFolder(CryptCarot.EncryptFilename(foldername), WeekDepend, parentJob);

            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: orgmakejob);
            job.DisplayName = "Make folder : " + foldername;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var item))
                {
                    j.ProgressStr = "Make folder...";
                    j.Progress = -1;


                    var newitem = new CarotCryptSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

                    j.Result = newitem;

                    SetUpdate(remoteTarget);
                }
                j.ProgressStr = "Done";
                j.Progress = 1;
            });
            return job;
        }

        internal void RemoveItem(string ID)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem(CarotCryptSystem)] " + ID);
            if (pathlist.TryRemove(ID, out CarotCryptSystemItem target))
            {
                if (target != null)
                {
                    var children = target.Children?.ToArray();
                    foreach (var child in children)
                    {
                        RemoveItem(child.ID);
                    }
                    foreach (var p in target.Parents)
                    {
                        p?.SetChildren(p.Children?.Where(x => x?.ID != target.ID));
                    }
                }
            }
        }

        public override Job<IRemoteItem> DeleteItem(IRemoteItem deleteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DeleteItem(CarotCryptSystem)] " + deleteTarget.Path);

            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Trash,
                depends: (deleteTarget as CarotCryptSystemItem).orgItem.DeleteItem(WeekDepend, prevJob));
            job.DisplayName = "Trash Item : " + deleteTarget.ID;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                j.ProgressStr = "Delete...";
                j.Progress = -1;

                var parent = deleteTarget.Parents.First();
                RemoveItem(deleteTarget.ID);

                j.Result = parent;
                j.ProgressStr = "Done";
                j.Progress = 1;
                SetUpdate(parent);
            });
            return job;
        }
        

        public override Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget,long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(CarotCryptSystem)] " + remoteTarget.Path);
            var djob = (remoteTarget as CarotCryptSystemItem).orgItem.DownloadItemRawJob(offset, WeekDepend, hidden, prevJob);

            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: djob);
            job.DisplayName = "Download item:" + remoteTarget.Name;
            job.ProgressStr = "wait for system...";
            job.ForceHidden = hidden;
            JobControler.Run<Stream>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var stream))
                {
                    j.Progress = -1;
                    j.Result = new CryptCarotDAV.CryptCarotDAV_DecryptStream(CryptCarot, stream, offset, offset, (remoteTarget as CarotCryptSystemItem).orgItem.Size ?? 0);
                    j.Progress = 1;
                    j.ProgressStr = "ready";
                }
            });
            return job;
        }

        public override Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DownloadItem(CarotCryptSystem)] " + remoteTarget.Path);
            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            job.DisplayName = "Download item:" + remoteTarget.Name;
            job.ProgressStr = "wait for system...";
            job.WeekDepend = WeekDepend;
            JobControler.Run<Stream>(job, (j) =>
            {
                j.Result = new ProjectUtil.SeekableStream(remoteTarget);
                j.Progress = 1;
                j.ProgressStr = "ready";
            });
            return job;
        }

        public override Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            filename = ItemControl.GetOrgFilename(filename);

            TSviewCloudConfig.Config.Log.LogOut("[UploadFile(CarotCryptSystem)] " + filename);
            var filesize = new FileInfo(ItemControl.GetLongFilename(filename)).Length;
            var short_filename = Path.GetFileName(ItemControl.GetLongFilename(filename));

            var filestream = new FileStream(ItemControl.GetLongFilename(filename), FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024);
            var job = UploadStream(filestream, remoteTarget, short_filename, filesize, WeekDepend, parentJob);

            return job;
        }

        public override Job<IRemoteItem> UploadStream(Stream source, IRemoteItem remoteTarget, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[UploadStream(CarotCryptSystem)] " + uploadname);
            streamsize += (CryptCarotDAV.BlockSizeByte + CryptCarotDAV.CryptFooterByte + CryptCarotDAV.CryptFooterByte);
            var cname = CryptCarot.EncryptFilename(uploadname);
            var cstream = new CryptCarotDAV.CryptCarotDAV_CryptStream(CryptCarot, source);

            TSviewCloudConfig.Config.Log.LogOut("[Upload] File: {0} -> {1}", uploadname, cname);

            var job = (remoteTarget as CarotCryptSystemItem).orgItem.UploadStream(cstream, cname, streamsize, WeekDepend, parentJob);
            job.DisplayName = uploadname;

            var clean = JobControler.CreateNewJob<IRemoteItem>(JobClass.Clean, depends: job);
            clean.DoAlways = true;
            JobControler.Run<IRemoteItem>(clean, (j) =>
            {
                if (job.IsCanceled) return;

                var result = clean.ResultOfDepend[0];
                if (result.TryGetTarget(out var item) && item != null)
                {
                    var newitem = new CarotCryptSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

                    j.Result = newitem;

                    SetUpdate(remoteTarget);
                }
            });
            return clean;
        }

        protected override Job<IRemoteItem> MoveItemOnServer(IRemoteItem moveItem, IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[MoveItemOnServer(CarotCryptSystem)] " + moveItem.FullPath);
            var job = (moveItem as CarotCryptSystemItem).orgItem.MoveItem((moveToItem as CarotCryptSystemItem).orgItem, WeekDepend, prevJob);

            var waitjob = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: job);
            JobControler.Run<IRemoteItem>(waitjob, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var prevresult))
                {
                    j.Result = prevresult;

                    SetUpdate(moveItem);
                    SetUpdate(moveToItem);
                }
            });
            return waitjob;
        }
    }
}
