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
        internal IRemoteItem orgItem;

        private string decryptedName;
        private string decryptedPath;

        public CarotCryptSystemItem() : base()
        {

        }

        public CarotCryptSystemItem(IRemoteServer server, IRemoteItem orgItem, params IRemoteItem[] parent) : base(server, parent)
        {
            if (!(parent?.Length > 0)) isRoot = true;

            this.orgItem = orgItem;
            orgpath = orgItem.FullPath;
            itemtype = orgItem.ItemType;
            size = orgItem?.Size - (CryptCarotDAV.BlockSizeByte + CryptCarotDAV.CryptFooterByte + CryptCarotDAV.CryptFooterByte);
            modifiedDate = orgItem.ModifiedDate;
            createdDate = orgItem.CreatedDate;

            decryptedName = (_server as CarotCryptSystem).CryptCarot.DecryptFilename(orgItem.Name) ?? "";
            decryptedPath = OrgPathToPath(orgpath);
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
            _server = server;
            orgItem = RemoteServerFactory.PathToItem(orgpath);
            Interlocked.CompareExchange(ref _children, new ConcurrentDictionary<string, IRemoteItem>(), null);
            _parents = parentIDs?.Select(x => _server.PeakItem(OrgPathToPath(x))).ToArray();
            decryptedPath = OrgPathToPath(orgpath);
            if (orgItem == null)
            {
                (_server as CarotCryptSystem)?.RemoveItem(Path);
                return;
            }
            decryptedName = (_server as CarotCryptSystem).CryptCarot.DecryptFilename(orgItem.Name) ?? "";
            if (ChildrenIDs != null)
            {
                Parallel.ForEach(ChildrenIDs.ToDictionary(k => OrgPathToPath(k), v => _server.PeakItem(OrgPathToPath(v))), (s) =>
                {
                    _children.AddOrUpdate(s.Key, (k) => s.Value, (k, v) => s.Value);
                });
            }
        }

    }

    [DataContract]
    public class CarotCryptSystem : RemoteServerBase
    {
        [DataMember(Name = "CryptNameHeader")]
        private string cryptNameHeader;
        [DataMember(Name = "CryptRootPath")]
        internal string cryptRootPath;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, CarotCryptSystemItem> pathlist;

        [DataMember(Name = "Password")]
        public string _DrivePassword;

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

            _IsReady = true;
            TSviewCloudConfig.Config.Log.LogOut("[Add] CarotCryptSystem {0} as {1}", cryptRootPath, Name);
            return true;
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] LocalSystem {0} as {1}", cryptRootPath, Name);
            CryptCarot = new CryptCarotDAV(cryptNameHeader)
            {
                Password = DrivePassword
            };

            var job = JobControler.CreateNewJob();
            job.DisplayName = "CryptCarotDAV";
            job.ProgressStr = "waiting parent";

            JobControler.Run(job, (j) =>
            {
                job.ProgressStr = "Loading...";
                job.Progress = -1;

                if (pathlist == null)
                {
                    pathlist = new ConcurrentDictionary<string, CarotCryptSystemItem>();
                    var root = new CarotCryptSystemItem(this, RemoteServerFactory.PathToItem(cryptRootPath), null);
                    pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                }
                else
                {
                    try
                    {
                        Parallel.ForEach(pathlist.Values.ToArray(), (x) => x.FixChain(this));
                    }
                    catch { }
                }

                job.ProgressStr = "Done";
                job.Progress = 1;

                _IsReady = true;
            });
        }

        public override IRemoteItem PeakItem(string path)
        {
            try
            {
                return pathlist[path];
            }
            catch
            {
                return null;
            }
        }
        protected override void EnsureItem(string path, int depth = 0)
        {
            var item = pathlist[path];
            if (item.ItemType == RemoteItemType.Folder)
                LoadItems(item.Path, depth);
            item = pathlist[path];
        }

        private CarotCryptSystemItem updateItemChain(string key, CarotCryptSystemItem olditem, CarotCryptSystemItem newitem)
        {
            foreach (var p in olditem.Parents)
            {
                IRemoteItem tmp;
                p.Children.TryRemove(key, out tmp);
            }

            newitem.SetChildren(olditem.Children.Values);

            foreach (var c in olditem.Children.Values)
            {
                c.ChangeParent(olditem, newitem);
            }

            return newitem;
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

        private void LoadItems(string path, int depth = 0)
        {
            if (depth < 0) return;
            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(CarotCryptSystem)] " + path);

            var orgitem = RemoteServerFactory.PathToItem(cryptRootPath + "/" + pathToCryptedpath(path), ReloadType.Reload);
            if(orgitem.Children != null && orgitem.Children.Count > 0)
            {
                var ret = new List<CarotCryptSystemItem>();
                Parallel.ForEach(
                    orgitem.Children.Values,
                    () => new List<CarotCryptSystemItem>(),
                    (x, state, local) =>
                    {
                        if (!x.Name.StartsWith(cryptNameHeader)) return local;

                        var item = new CarotCryptSystemItem(this, x, pathlist[path]);
                        pathlist.AddOrUpdate(item.Path, (k) => item, (k, v) =>
                        {
                            return updateItemChain(k, v, item);
                        });
                        local.Add(item);
                        return local;
                    },
                     (result) =>
                     {
                         lock (ret)
                             ret.AddRange(result);
                     }
                );
                pathlist[path].SetChildren(ret);
                if (depth > 0)
                    Parallel.ForEach(pathlist[path].Children.Values, (x) => { LoadItems(x.Path, depth - 1); });

            }
            else
            {
                pathlist[path].SetChildren(null);
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
 
            var parent = pathlist[remoteTarget.Path];
            var orgmakejob = parent.orgItem.MakeFolder(CryptCarot.EncryptFilename(foldername), WeekDepend, parentJob);

            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: orgmakejob);
            job.DisplayName = "Make folder : " + foldername;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run(job, (j) =>
            {
                job.ProgressStr = "Make folder...";
                job.Progress = -1;

                var item = job.ResultOfDepend[0];

                var newitem = new CarotCryptSystemItem(this, item, remoteTarget);
                pathlist.AddOrUpdate(newitem.Path, (k) => newitem, (k, v) =>
                {
                    return updateItemChain(k, v, newitem);
                });

                remoteTarget.Children.AddOrUpdate(newitem.Path, newitem, (k, v) => newitem);

                job.Result = newitem;

                job.ProgressStr = "Done";
                job.Progress = 1;

                SetUpdate(remoteTarget);
            });
            return job;
        }

        internal void RemoveItem(string path)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem(CarotCryptSystem)] " + path);
            if (pathlist.TryRemove(path, out CarotCryptSystemItem target))
            {
                var children = target?.Children.Values.ToArray();
                foreach (var child in children)
                {
                    RemoveItem(child.Path);
                }
                foreach (var p in target.Parents)
                {
                    p.Children?.TryRemove(path, out IRemoteItem tmp);
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
            JobControler.Run(job, (j) =>
            {
                job.ProgressStr = "Delete...";
                job.Progress = -1;

                var parent = deleteTarget.Parents[0];
                RemoveItem(deleteTarget.Path);

                job.Result = parent;
                job.ProgressStr = "Done";
                job.Progress = 1;
                SetUpdate(parent);
            });
            return job;
        }
        

        public override Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget,long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(CarotCryptSystem)] " + remoteTarget.Path);
            var djob = (remoteTarget as CarotCryptSystemItem).orgItem.DownloadItemRaw(offset, WeekDepend, hidden, prevJob);

            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: djob);
            job.DisplayName = "Download item:" + remoteTarget.Name;
            job.ProgressStr = "wait for system...";
            job.ForceHidden = hidden;
            JobControler.Run(job, (j) =>
            {
                (j as Job<Stream>).Progress = -1;
                (j as Job<Stream>).Result = new CryptCarotDAV.CryptCarotDAV_DecryptStream(CryptCarot, (j as Job<Stream>).ResultOfDepend[0], offset, offset, (remoteTarget as CarotCryptSystemItem).orgItem.Size ?? 0);
                (j as Job<Stream>).Progress = 1;
                (j as Job<Stream>).ProgressStr = "ready";
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
            JobControler.Run(job, (j) =>
            {
                (j as Job<Stream>).Result = new ProjectUtil.SeekableStream(remoteTarget);
                (j as Job<Stream>).Progress = 1;
                (j as Job<Stream>).ProgressStr = "ready";
            });
            return job;
        }

        public override Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[UploadFile(CarotCryptSystem)] " + filename);
            var filesize = new FileInfo(filename).Length;
            var short_filename = Path.GetFileName(filename);

            var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024);
            var job = UploadStream(filestream, remoteTarget, short_filename, filesize, WeekDepend, parentJob);

            var clean = JobControler.CreateNewJob<IRemoteItem>(JobClass.Clean, depends: job);
            clean.DoAlways = true;
            JobControler.Run(clean, (j) =>
            {
                clean.Result = clean.ResultOfDepend[0];
                filestream.Dispose();
            });
            return clean;
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

            var clean = JobControler.CreateNewJob<IRemoteItem>(JobClass.Clean, depends: job);
            clean.DoAlways = true;
            JobControler.Run(clean, (j) =>
            {
                clean.Result = clean.ResultOfDepend[0];
                cstream.Dispose();

                SetUpdate(remoteTarget);
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
            JobControler.Run(waitjob, (j) =>
            {
                waitjob.Result = waitjob.ResultOfDepend[0];

                SetUpdate(moveItem);
                SetUpdate(moveToItem);
            });
            return waitjob;
        }
    }
}
