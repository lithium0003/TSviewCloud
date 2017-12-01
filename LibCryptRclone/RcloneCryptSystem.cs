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
using System.Text.RegularExpressions;
using LibCryptRclone;

namespace TSviewCloudPlugin
{
    [DataContract]
    public class RcloneCryptSystemItem : RemoteItemBase
    {
        [DataMember(Name = "ID")]
        private string orgpath;

        internal byte[] nonce;
        public override bool IsReadyRead => nonce != null;


        internal virtual IRemoteItem orgItem
        {
            get
            {
                return RemoteServerFactory.PathToItem(orgpath).Result;
            }
        }

        private string decryptedName;
        private string decryptedPath;

        public override long? Size
        {
            get
            {
                return size = ((orgItem?.Size == null)? orgItem?.Size: CryptRclone.CalcDecryptedSize(orgItem?.Size ?? 0));
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
        public override DateTime? AccessDate
        {
            get
            {
                return accessDate = orgItem?.AccessDate;
            }
        }

        public RcloneCryptSystemItem() : base()
        {

        }

        public RcloneCryptSystemItem(IRemoteServer server, IRemoteItem orgItem, params IRemoteItem[] parent) : base(server, parent)
        {
            if (!(parent?.Length > 0)) isRoot = true;

            orgpath = orgItem.FullPath;
            itemtype = orgItem.ItemType;
            size = ((orgItem.Size == null) ? orgItem.Size : CryptRclone.CalcDecryptedSize(orgItem.Size.Value));
            modifiedDate = orgItem.ModifiedDate;
            createdDate = orgItem.CreatedDate;

            var encryptor = (_server as RcloneCryptSystem).Encrypter;
            if (encryptor.IsEncryptedName)
            {
                decryptedName = encryptor.DecryptName(orgItem.Name) ?? "";
                if (decryptedName == "" && !isRoot) throw new FileNotFoundException("filename dedoce error");
            }
            else
            {
                if(itemtype == RemoteItemType.Folder)
                {
                    decryptedName = orgItem.Name;
                }
                else
                {
                    if (orgItem.Name.EndsWith(CryptRclone.encryptedSuffix))
                        decryptedName = orgItem.Name.Substring(0, orgItem.Name.Length - CryptRclone.encryptedSuffix.Length);
                    else
                        throw new FileNotFoundException("filename dedoce error");
                }
            }
            decryptedPath = OrgPathToPath(orgpath);

            if (isRoot) SetParent(this);
        }

        public override string ID => orgpath;

        private string OrgPathToPath(string path)
        {
            if (string.IsNullOrEmpty(path) || (_server as RcloneCryptSystem).cryptRootPath == path)
                return "";
                
            if (!path.StartsWith((_server as RcloneCryptSystem).cryptRootPath)) throw new Exception("internal error: RcloneCryptSystemItem rootpath");

            var ret = new List<string>();
            path = path.Substring((_server as RcloneCryptSystem).cryptRootPath.Length);

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
                    ret.Add((_server as RcloneCryptSystem).Encrypter.DecryptName(m.Groups["current"].Value));
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
                var orgItem = RemoteServerFactory.PathToItem(orgpath).Result;
                if (orgItem == null)
                {
                    (_server as RcloneCryptSystem)?.RemoveItem(ID);
                    return;
                }
                decryptedPath = OrgPathToPath(orgpath);
                decryptedName = (_server as RcloneCryptSystem).Encrypter.DecryptName(orgItem.Name) ?? "";
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(ID);
            }
            base.FixChain(server);
        }

    }

    [DataContract]
    public class RcloneCryptSystem : RemoteServerBase
    {
        [DataMember(Name = "CryptRootPath")]
        internal string cryptRootPath;
        [DataMember(Name = "Password")]
        public string _DrivePassword;
        [DataMember(Name = "Salt")]
        public string _DriveSalt;
        [DataMember(Name = "FilenameEncryption")]
        public bool FilenameEncryption;

        private ConcurrentDictionary<string, RcloneCryptSystemItem> pathlist;

        private ConcurrentDictionary<string, ManualResetEventSlim> loadinglist;

        const string hidden_pass = "Rclone Drive Password";
        public string DrivePassword
        {
            get
            {
                return TSviewCloudConfig.Config.Decrypt(_DrivePassword, hidden_pass);
            }
            set
            {
                _DrivePassword = TSviewCloudConfig.Config.Encrypt(value, hidden_pass);
            }
        }
        public string DriveSalt
        {
            get
            {
                return TSviewCloudConfig.Config.Decrypt(_DriveSalt, hidden_pass);
            }
            set
            {
                _DriveSalt = TSviewCloudConfig.Config.Encrypt(value, hidden_pass);
            }
        }

        internal CryptRclone Encrypter;

        public RcloneCryptSystem()
        {
            pathlist = new ConcurrentDictionary<string, RcloneCryptSystemItem>();
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
        }

        public async override Task<bool> Add()
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
            Encrypter = new CryptRclone(pass.Password, pass.Salt)
            {
                IsEncryptedName = pass.FilenameEncryption,
            };
            DrivePassword = pass.Password;
            DriveSalt = pass.Salt;
            FilenameEncryption = pass.FilenameEncryption;

            cryptRootPath = picker.SelectedItem.FullPath;
            _dependService = picker.SelectedItem.Server;
            var root = new RcloneCryptSystemItem(this, picker.SelectedItem, null);
            pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            await EnsureItem("", 2);

            _IsReady = true;
            TSviewCloudConfig.Config.Log.LogOut("[Add] RcloneCryptSystem {0} as {1}", cryptRootPath, Name);
            return true;
        }

        public override void ClearCache()
        {
            _IsReady = false;
            pathlist.Clear();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "Initialize RcloneCrypt";
            job.ProgressStr = "Initialize...";
            JobControler.Run(job, async (j) =>
            {
                job.Progress = -1;

                job.ProgressStr = "waiting for base system...";
                while (!RemoteServerFactory.ServerList[_dependService].IsReady)
                    Task.Delay(1000, j.Ct).Wait(j.Ct);

                job.ProgressStr = "loading...";
                var host = await RemoteServerFactory.PathToItem(cryptRootPath);
                if (host == null) return;
                var root = new RcloneCryptSystemItem(this, host, null);
                pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                await EnsureItem("", 2);
                _IsReady = true;

                job.Progress = 1;
                job.ProgressStr = "done.";
            });
        }


        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] RcloneCryptSystem {0} as {1}", cryptRootPath, Name);
            Encrypter = new CryptRclone(DrivePassword, DriveSalt)
            {
                IsEncryptedName = FilenameEncryption,
            };
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "CryptRclone";
            job.ProgressStr = "waiting parent";

            JobControler.Run(job, async (j) =>
            {
                j.ProgressStr = "Loading...";
                j.Progress = -1;

                try
                {
                    int waitcount = 500;
                    while (!(RemoteServerFactory.ServerList.Keys.Contains(_dependService) && RemoteServerFactory.ServerList[_dependService].IsReady))
                    {
                        if(RemoteServerFactory.ServerList.Keys.Contains(_dependService))
                            await Task.Delay(1, j.Ct);
                        else
                            await Task.Delay(1000,j.Ct);

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
                    pathlist = new ConcurrentDictionary<string, RcloneCryptSystemItem>();
                    var root = new RcloneCryptSystemItem(this, await RemoteServerFactory.PathToItem(cryptRootPath), null);
                    pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                    await EnsureItem("", 2);
                }
                else
                {
                    Parallel.ForEach(pathlist.Values.ToArray(), 
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (x) => x.FixChain(this));
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

        protected async override Task EnsureItem(string ID, int depth = 0)
        {
            if (ID == RootID) ID = "";
            try
            {
                TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(RcloneCryptSystem)] " + ID);
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    await LoadItems(ID, depth);
                item = pathlist[ID];
            }
            catch
            {
                await LoadItems(ID, depth);
            }
        }
        public async override Task<IRemoteItem> ReloadItem(string ID)
        {
            if (ID == RootID) ID = "";
            try
            {
                TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(RcloneCryptSystem)] " + ID);
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    await LoadItems(ID, 2, true);
                item = pathlist[ID];
            }
            catch
            {
                await LoadItems(ID, 2, true);
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
                    if (Encrypter.IsEncryptedName)
                    {
                        ret.Add(Encrypter.EncryptName(m.Groups["current"].Value));
                    }
                    else
                    {
                        var plain = m.Groups["current"].Value;
                        ret.Add((path == "") ? plain + CryptRclone.encryptedSuffix : plain);
                    }
                }
            }
            return string.Join("/", ret);
        }

        private async Task LoadItems(string ID, int depth = 0, bool deep = false)
        {
            if (depth < 0) return;
            ID = ID ?? "";

            bool master = true;
            loadinglist.AddOrUpdate(ID, new ManualResetEventSlim(false), (k, v) =>
            {
                if (v.IsSet)
                    return new ManualResetEventSlim(false);

                master = false;
                return v;
            });

            if (!master)
            {
                while (loadinglist.TryGetValue(ID, out var tmp) && tmp != null)
                {
                    await Task.Run(() => tmp.Wait());
                }
                return;
            }
            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(RcloneCryptSystem)] " + ID);

            try
            {
                var orgID = (string.IsNullOrEmpty(ID)) ? cryptRootPath : ID;
                if (!orgID.StartsWith(cryptRootPath))
                {
                    throw new ArgumentException("ID is not in root path", "ID");
                }
                var orgitem = await RemoteServerFactory.PathToItem(orgID, (deep) ? ReloadType.Reload : ReloadType.Cache);
                if (orgitem?.Children != null && orgitem.Children?.Count() != 0)
                {
                    var ret = new List<RcloneCryptSystemItem>();
                    Parallel.ForEach(
                        orgitem.Children,
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        () => new List<RcloneCryptSystemItem>(),
                        (x, state, local) =>
                        {
                            var child = RemoteServerFactory.PathToItem(x.FullPath, (deep) ? ReloadType.Reload : ReloadType.Cache).Result;
                            if (child == null)
                                return local;

                            try
                            {
                                var item = new RcloneCryptSystemItem(this, child, pathlist[ID]);
                                pathlist.AddOrUpdate(item.ID, (k) => item, (k, v) => item);
                                local.Add(item);
                            }
                            catch { }

                            return local;
                        },
                         (result) =>
                         {
                             lock (ret)
                                 ret.AddRange(result);
                         }
                    );
                    pathlist[ID].SetChildren(ret);
                }
                else
                {
                    pathlist[ID].SetChildren(null);

                }
            }
            finally
            {
                ManualResetEventSlim tmp2;
                while (!loadinglist.TryRemove(ID, out tmp2))
                    await Task.Delay(10);
                tmp2.Set();
            }
            if (depth > 0)
                Parallel.ForEach(pathlist[ID].Children,
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => { LoadItems(x.ID, depth - 1).Wait(); });
        }

        public override Icon GetIcon()
        {
            return LibCryptRclone.Properties.Resources.rclone;
        }

        public override string GetServiceName()
        {
            return "RcloneCrypt";
        }

        public override void Init()
        {
            RemoteServerFactory.Register(GetServiceName(), typeof(RcloneCryptSystem));
        }

        public override Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            try
            {
                var check = CheckUpload(remoteTarget, foldername, null, WeekDepend, parentJob);
                if (check != null)
                {
                    WeekDepend = false;
                    parentJob = new[] { check };
                }
            }
            catch
            {
                var mkjob = JobControler.CreateNewJob<IRemoteItem>(
                    type: JobClass.RemoteOperation,
                    depends: parentJob);
                mkjob.WeekDepend = WeekDepend;
                mkjob.ForceHidden = true;
                JobControler.Run<IRemoteItem>(mkjob, (j) =>
                {
                    j.Result = remoteTarget.Children.Where(x => x.Name == foldername).FirstOrDefault();
                });
                return mkjob;
            }

            TSviewCloudConfig.Config.Log.LogOut("[MakeFolder(RcloneCryptSystem)] " + foldername);
 
            var parent = pathlist[(remoteTarget.ID== cryptRootPath)? "": remoteTarget.ID];
            var dirname = (Encrypter.IsEncryptedName) ? Encrypter.EncryptName(foldername) : foldername;
            var orgmakejob = parent.orgItem.MakeFolder(dirname, WeekDepend, parentJob);

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


                    var newitem = new RcloneCryptSystemItem(this, item, remoteTarget);
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
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem(RcloneCryptSystem)] " + ID);
            if (pathlist.TryRemove(ID, out RcloneCryptSystemItem target))
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

            TSviewCloudConfig.Config.Log.LogOut("[DeleteItem(RcloneCryptSystem)] " + deleteTarget.Path);

            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Trash,
                depends: (deleteTarget as RcloneCryptSystemItem).orgItem.DeleteItem(WeekDepend, prevJob));
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

            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(RcloneCryptSystem)] " + remoteTarget.Path);

            var chunk_num = offset / CryptRclone.blockDataSize;
            long newoffset;
            if (offset < CryptRclone.blockDataSize)
                newoffset = 0;
            else
                newoffset = CryptRclone.fileHeaderSize + CryptRclone.chunkSize * chunk_num;

            var rTarget = remoteTarget as RcloneCryptSystemItem;
            var djob = rTarget.orgItem.DownloadItemRawJob(newoffset, WeekDepend, hidden, prevJob);

            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: djob);
            job.DisplayName = "Download item:" + remoteTarget.Name;
            job.ProgressStr = "wait for system...";
            job.ForceHidden = hidden;
            job.WeekDepend = false;

            JobControler.Run<Stream>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var stream))
                {
                    j.Progress = -1;
                    var cstream = new CryptRclone.CryptRclone_DeryptStream(Encrypter, stream, offset, newoffset, rTarget.orgItem.Size ?? 0, rTarget.nonce);
                    j.Result = cstream;
                    rTarget.nonce = cstream.Nonce;
                    j.Progress = 1;
                    j.ProgressStr = "ready";
                }
            });
            return job;
        }

        public override Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DownloadItem(RcloneCryptSystem)] " + remoteTarget.Path);
            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            job.DisplayName = "Download item:" + remoteTarget.Name;
            job.ProgressStr = "wait for system...";
            job.WeekDepend = WeekDepend;
            JobControler.Run<Stream>(job, (j) =>
            {
                j.Result = new ProjectUtil.SeekableStream(remoteTarget, j.Ct);
                j.Progress = 1;
                j.ProgressStr = "ready";
            });
            return job;
        }

 
        public override Job<IRemoteItem> UploadStream(Stream source, IRemoteItem remoteTarget, string uploadname, long streamsize, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            try
            {
                var check = CheckUpload(remoteTarget, uploadname, streamsize, WeekDepend, parentJob);
                if (check != null)
                {
                    WeekDepend = false;
                    parentJob = new[] { check };
                }
            }
            catch
            {
                return null;
            }

            TSviewCloudConfig.Config.Log.LogOut("[UploadStream(RcloneCryptSystem)] " + uploadname);
            var cname = (Encrypter.IsEncryptedName)? Encrypter.EncryptName(uploadname): uploadname + CryptRclone.encryptedSuffix;
            var cstream = new CryptRclone.CryptRclone_CryptStream(Encrypter, source, streamsize);
            streamsize = CryptRclone.CalcEncryptedSize(streamsize);

            TSviewCloudConfig.Config.Log.LogOut("[Upload] File: {0} -> {1}", uploadname, cname);

            var job = (remoteTarget as RcloneCryptSystemItem).orgItem?.UploadStream(cstream, cname, streamsize, WeekDepend, parentJob);
            if (job == null)
            {
                LogFailed(remoteTarget.FullPath + "/" + uploadname, "upload error: base file upload failed");
                cstream.Dispose();
                return null;
            }
            job.DisplayName = uploadname;

            var clean = JobControler.CreateNewJob<IRemoteItem>(JobClass.Clean, depends: job);
            clean.DoAlways = true;
            JobControler.Run<IRemoteItem>(clean, (j) =>
            {
                if (job.IsCanceled) return;

                var result = clean.ResultOfDepend[0];
                if (result.TryGetTarget(out var item) && item != null)
                {
                    var newitem = new RcloneCryptSystemItem(this, item, remoteTarget);
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

            TSviewCloudConfig.Config.Log.LogOut("[MoveItemOnServer(RcloneCryptSystem)] " + moveItem.FullPath);
            var job = (moveItem as RcloneCryptSystemItem).orgItem.MoveItem((moveToItem as RcloneCryptSystemItem).orgItem, WeekDepend, prevJob);

            var waitjob = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: job);
            JobControler.Run<IRemoteItem>(waitjob, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var prevresult))
                {
                    j.Result = prevresult;
                }
                var oldparent = moveItem.Parents.First();
                SetUpdate(oldparent);
                SetUpdate(moveToItem);
            });
            return waitjob;
        }

        public override Job<IRemoteItem> RenameItem(IRemoteItem targetItem, string newName, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[RenameItem(RcloneCryptSystem)] " + targetItem.FullPath);
            var cname = (Encrypter.IsEncryptedName) ? Encrypter.EncryptName(newName) : (targetItem.ItemType == RemoteItemType.File)? newName + CryptRclone.encryptedSuffix : newName;
            var job = (targetItem as RcloneCryptSystemItem).orgItem.RenameItem(cname, WeekDepend, prevJob);

            var waitjob = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: job);
            JobControler.Run<IRemoteItem>(waitjob, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var prevresult))
                {
                    j.Result = prevresult;
                }
                var parent = targetItem.Parents.First();
                SetUpdate(parent);
            });
            return waitjob;
        }

        public override Job<IRemoteItem> ChangeAttribItem(IRemoteItem targetItem, IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[ChangeAttribItem(RcloneCryptSystem)] " + targetItem.FullPath);
            var job = (targetItem as RcloneCryptSystemItem).orgItem.ChangeAttribItem(newAttrib, WeekDepend, prevJob);

            var waitjob = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: job);
            JobControler.Run<IRemoteItem>(waitjob, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var prevresult))
                {
                    j.Result = prevresult;
                }
                var parent = targetItem.Parents.First();
                SetUpdate(parent);
            });
            return waitjob;
        }

    }
}
