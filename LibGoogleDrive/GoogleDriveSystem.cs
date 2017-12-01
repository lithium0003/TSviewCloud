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
using LibGoogleDrive;
using System.Net.Http;

namespace TSviewCloudPlugin
{
    [DataContract]
    public class GoogleDriveSystemItem : RemoteItemBase
    {
        [DataMember(Name = "ID")]
        private string id;
        [DataMember(Name = "Name")]
        private string name;
        [DataMember(Name = "LastLoaded")]
        DateTime? lastLoaded;

        public GoogleDriveSystemItem() : base()
        {

        }

        public GoogleDriveSystemItem(IRemoteServer server, FileMetadata_Info info, params IRemoteItem[] parent) : base(server, parent)
        {
            SetFileds(info);
            if (parent?.Length > 0) isRoot = false;
        }

        public void SetFileds(FileMetadata_Info info)
        {
            id = info.id;
            name = info.name;
            itemtype = (info.IsFolder) ? RemoteItemType.Folder : RemoteItemType.File;
            size = info.size;
            modifiedDate = info.ModifiedDate;
            createdDate = info.CreatedDate;
            hash = info.md5Checksum;
            if (hash != null) hash = "MD5:" + hash;
            parentIDs = info.parents;

            isRoot = (parentIDs == null || parentIDs.Length == 0);
            if (isRoot) SetParent(this);

            Age = DateTime.Now;
        }

        public void AddChild(IRemoteItem newchild)
        {
            ChildrenIDs = ChildrenIDs?.Concat(new[] { newchild.ID }).ToArray() ?? new[] { newchild.ID };
            ChildrenIDs = ChildrenIDs.Distinct().ToArray();
            Age = DateTime.Now;
        }

        public void RemoveChild(IRemoteItem child)
        {
            ChildrenIDs = ChildrenIDs?.Except(new[] { child.ID }).ToArray() ?? new[] { child.ID };
            Age = DateTime.Now;
        }
        public void RemoveChild(string child)
        {
            ChildrenIDs = ChildrenIDs?.Except(new[] { child }).ToArray() ?? new[] { child };
            Age = DateTime.Now;
        }

        public override void FixChain(IRemoteServer server)
        {
            _server = server;
            base.FixChain(server);
        }

        public override string ID => id;
        public override string Path => (isRoot) ? "" : Parents.First().Path + ((Parents.First().Path == "") ? "" : "/") + PathItemName;
        public override string Name => name;
        public override string PathItemName => Uri.EscapeDataString(Name);

        public string[] RawParents => parentIDs;

        public DateTime? LastLoaded { get => lastLoaded; set => lastLoaded = value; }
    }

    [DataContract]
    public class GoogleDriveSystem: RemoteServerBase
    {
        [DataMember(Name = "RootID")]
        private string rootID;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, GoogleDriveSystemItem> pathlist;

        private ConcurrentDictionary<string, ManualResetEventSlim> loadinglist;

        [DataMember(Name = "RefreshToken")]
        public string _refresh_token;

        const string hidden_pass = "GoogleDrive Tokens";
        public string Refresh_Token
        {
            get
            {
                return TSviewCloudConfig.Config.Decrypt(_refresh_token, hidden_pass);
            }
            set
            {
                _refresh_token = TSviewCloudConfig.Config.Encrypt(value, hidden_pass);
            }
        }

 
        private GoogleDrive Drive;

        public GoogleDriveSystem()
        {
            pathlist = new ConcurrentDictionary<string, GoogleDriveSystemItem>();
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] GoogleDriveSystem {0}", Name);

            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
            Drive = new GoogleDrive()
            {
                Auth = new AuthKeys() { refresh_token = Refresh_Token },
            };
            var job = JobControler.CreateNewJob();
            job.DisplayName = "login GoogleDrive";
            job.ProgressStr = "login...";
            JobControler.Run(job, async (j) =>
            {
                job.Progress = -1;

                await Drive.EnsureToken(j.Ct);

                job.ProgressStr = "loading cache...";
                if (pathlist == null)
                {
                    pathlist = new ConcurrentDictionary<string, GoogleDriveSystemItem>();

                    var rootitem = Drive.FilesGet(ct: j.Ct).Result;

                    var root = new GoogleDriveSystemItem(this, rootitem, null);
                    rootID = root.ID;
                    pathlist[""] = pathlist[rootID] = root;

                    await LoadItems(rootID, 2);
                }
                else
                {
                    Parallel.ForEach(
                        pathlist.Values.ToArray(),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (x) => x.FixChain(this));
                }

                _IsReady = true;

                job.Progress = 1;
                job.ProgressStr = "done.";
            });
        }

        protected override string RootID => rootID;

        public override IRemoteItem this[string ID]
        {
            get
            {
                try
                {
                    if (ID == null) return null;
                    if (ID == "") ID = RootID;
                    return PeakItem(ID);
                }
                catch
                {
                    return null;
                }
            }
        }

        public override IRemoteItem PeakItem(string ID)
        {
            if (ID == "") ID = RootID;
            try
            {
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder && item.LastLoaded == null)
                    LoadItems(ID, 0).Wait();
                return pathlist[ID];
            }
            catch
            {
                return null;
            }
        }
        protected async override Task EnsureItem(string ID, int depth = 0)
        {
            if (ID == "") ID = RootID;
            TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(GoogleDriveSystem)] " + ID);
            try
            {
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
            if (ID == "") ID = RootID;
            TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(GoogleDriveSystem)] " + ID);
            try
            {
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    await LoadItems(ID, 2);
                item = pathlist[ID];
            }
            catch
            {
                await LoadItems(ID, 2);
            }
            return PeakItem(ID);
        }


        public override void Init()
        {
            RemoteServerFactory.Register(GetServiceName(), typeof(GoogleDriveSystem));
        }

        public override string GetServiceName()
        {
            return "GoogleDrive";
        }

        public override Icon GetIcon()
        {
            return LibGoogleDrive.Properties.Resources.GoogleDrive;
        }


        private async Task<bool> InitializeDrive()
        {
            var formlogin = new FormLogin(this);
            var ret = false;

            var Authkey = formlogin.Login();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "login GoogleDrive";
            job.ProgressStr = "login...";
            JobControler.Run(job, (j) =>
            {
                job.Progress = -1;
                if (Authkey != null && !string.IsNullOrEmpty(Authkey.access_token))
                {
                    job.ProgressStr = "initialize connection...";
                    Drive = new GoogleDrive()
                    {
                        Auth = Authkey,
                    };
                    Drive.EnsureToken(j.Ct).Wait(j.Ct);

                    ret = true;
                }
                job.Progress = 1;
                job.ProgressStr = "done.";
            });
            Cursor.Current = Cursors.WaitCursor;
            await job.WaitTask();
            return ret;
        }


        public override void Disconnect()
        {
            base.Disconnect();
            GoogleDrive.RevokeToken(Drive.Auth).Wait();
        }

        private async Task LoadItems(string ID, int depth = 0)
        {
            if (depth < 0) return;
            ID = ID ?? RootID;

            if (pathlist[ID].ItemType == RemoteItemType.File) return;

            if (DateTime.Now - pathlist[ID].LastLoaded < TimeSpan.FromSeconds(15))
            {
                return;
            }

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

            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(GoogleDriveSystem)] " + ID);

            try
            {
                var job = JobControler.CreateNewJob();
                job.DisplayName = "Loading GoogleDrive Item:" + ID;
                job.ProgressStr = "Initialize...";
                JobControler.Run(job, (j) =>
                {
                    job.Progress = -1;

                    job.ProgressStr = "Loading children...";

                    var me = Drive.FilesGet(ID, ct: j.Ct).Result;
                    if (me == null || (me.trashed ?? false))
                    {
                        RemoveItem(ID);
                        return;
                    }
                    pathlist[ID].SetFileds(me);

                    var children = Drive.ListChildren(ID, ct: j.Ct).Result;
                    if (children != null)
                    {
                        var ret = new List<GoogleDriveSystemItem>();
                        Parallel.ForEach(
                            children,
                            new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                            () => new List<GoogleDriveSystemItem>(),
                            (x, state, local) =>
                            {
                                if (x.trashed ?? false) return local;
                                var item = new GoogleDriveSystemItem(this, x, pathlist[ID]);
                                pathlist.AddOrUpdate(item.ID, (k) => item, (k, v) => { v.SetFileds(x); return v; });
                                local.Add(pathlist[item.ID]);
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
                    pathlist[ID].LastLoaded = DateTime.Now;

                    job.Progress = 1;
                    job.ProgressStr = "done";
                });
                await job.WaitTask();
            }
            catch
            {
                RemoveItem(ID);
            }
            finally
            {
                ManualResetEventSlim tmp3;
                while (!loadinglist.TryRemove(ID, out tmp3))
                    await Task.Delay(10);
                tmp3.Set();
            }

            if (depth > 0)
            {
                Parallel.ForEach(pathlist[ID].Children.Where(x => x.ItemType == RemoteItemType.Folder),
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => { LoadItems(x.ID, depth - 1).Wait(); });
            }
        }


        private void RemoveItemChain(GoogleDriveSystemItem item)
        {
            foreach (var p in item.Parents)
            {
                (p as GoogleDriveSystemItem).RemoveChild(item);
            }
            foreach (var c in item.Children)
            {
                RemoveItemChain(c as GoogleDriveSystemItem);
            }
        }

 
        public async override Task<bool> Add()
        {
            if(await InitializeDrive())
            {
                TSviewCloudConfig.Config.Log.LogOut("[Add] GoogleDriveSystem {0}", Name);

                var job = JobControler.CreateNewJob();
                job.DisplayName = "Initialize GoogleDrive";
                job.ProgressStr = "Initialize...";
                JobControler.Run(job, async (j) =>
                {
                    j.Progress = -1;

                    j.ProgressStr = "Loading root...";
                    var rootitem = Drive.FilesGet(ct: j.Ct).Result;

                    var root = new GoogleDriveSystemItem(this, rootitem, null);
                    rootID = root.ID;
                    pathlist[""] = pathlist[rootID] = root;

                    await LoadItems(rootID, 2);

                    _IsReady = true;

                    j.Progress = 1;
                    j.ProgressStr = "done.";


                    var job2 = JobControler.CreateNewJob();
                    job2.DisplayName = "Scan GoogleDrive";
                    job2.ProgressStr = "Scan...";
                    JobControler.Run(job2, async (j2) =>
                    {
                        j2.Progress = -1;

                        await ScanItems(pathlist[rootID], j2.Ct);

                        j2.Progress = 1;
                        j2.ProgressStr = "done.";
                    });
                });
                return true;
            }
            return false;
        }

        private async Task ScanItems(IRemoteItem baseitem, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();
            await LoadItems(baseitem.ID, 0);
            foreach(var i in baseitem.Children)
            {
                if(i.ItemType == RemoteItemType.Folder)
                    await ScanItems(i, ct);
            }
        }

        public override void ClearCache()
        {
            _IsReady = false;
            pathlist.Clear();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "Initialize GoogleDrive";
            job.ProgressStr = "Initialize...";
            JobControler.Run(job, async (j) =>
            {
                job.Progress = -1;

                var rootitem = Drive.FilesGet(ct: j.Ct).Result;

                var root = new GoogleDriveSystemItem(this, rootitem, null);
                rootID = root.ID;
                pathlist[""] = pathlist[rootID] = root;

                await LoadItems(rootID, 2);

                _IsReady = true;

                job.Progress = 1;
                job.ProgressStr = "done.";

                var job2 = JobControler.CreateNewJob();
                job2.DisplayName = "Scan GoogleDrive";
                job2.ProgressStr = "Scan...";
                JobControler.Run(job2, async (j2) =>
                {
                    j2.Progress = -1;

                    await ScanItems(pathlist[rootID], j2.Ct);

                    j2.Progress = 1;
                    j2.ProgressStr = "done.";
                });
            });
        }

        private void RemoveItem(string ID)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem] " + ID);
            if (pathlist.TryRemove(ID, out GoogleDriveSystemItem target))
            {
                if (target != null)
                {
                    var children = target.Children.ToArray();
                    foreach (var child in children)
                    {
                        RemoveItem(child.ID);
                    }
                    foreach (var p in target.Parents)
                    {
                        p?.SetChildren(p.Children.Where(x => x.ID != target.ID));
                    }
                }
            }
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

            TSviewCloudConfig.Config.Log.LogOut("[MakeFolder(GoogleDriveSystem)] " + foldername);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: parentJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "Make folder : " + foldername;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[MkFolder] " + foldername);

                j.ProgressStr = "Make folder...";
                j.Progress = -1;

                try
                {
                    var newremoteitem = Drive.CreateFolder(foldername, remoteTarget.ID, j.Ct).Result;
                    var newitem = pathlist[newremoteitem.id] = new GoogleDriveSystemItem(this, newremoteitem, null);

                    (remoteTarget as GoogleDriveSystemItem).AddChild(newitem);

                    j.Result = newitem;

                    j.ProgressStr = "Done";
                    j.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("Make folder Failed.", e);
                }
                SetUpdate(remoteTarget);
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

            TSviewCloudConfig.Config.Log.LogOut("[UploadStream(GoogleDriveSystem)] " + uploadname);
            var filesize = streamsize;
            var short_filename = uploadname;
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Upload,
                info: new JobControler.SubInfo
                {
                    type = JobControler.SubInfo.SubType.UploadFile,
                    size = filesize,
                },
                depends: parentJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = uploadname;
            job.ProgressStr = "wait for upload.";
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                j.Ct.ThrowIfCancellationRequested();

                j.ProgressStr = "Upload...";
                j.Progress = 0;

                try
                {
                    FileMetadata_Info newremoteitem = null;

                    using (source)
                    using (var th = new ThrottleUploadStream(source, j.Ct))
                    using (var f = new PositionStream(th))
                    {
                        f.PosChangeEvent += (src, evnt) =>
                        {
                            if (j.Ct.IsCancellationRequested) return;
                            var eo = evnt;
                            j.ProgressStr = eo.Log;
                            j.Progress = (double)eo.Position / eo.Length;
                            j.JobInfo.pos = eo.Position;
                        };

                        newremoteitem = Drive.UploadStream(f, remoteTarget.ID, uploadname, streamsize, j.Ct).Result;

                        if(newremoteitem == null)
                        {
                            j.ProgressStr = "Upload failed.";
                            j.Progress = double.NaN;
                        }
                        else
                        {
                            j.Progress = 1;
                            var newitem = pathlist[newremoteitem.id] = new GoogleDriveSystemItem(this, newremoteitem, null);
                            (remoteTarget as GoogleDriveSystemItem).AddChild(newitem);

                            j.Result = newitem;
                        }
                    }
                    source = null;

                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    j.ProgressStr = "Upload failed.";
                    j.Progress = double.NaN;
                    LogFailed(remoteTarget.FullPath + "/" + uploadname, "upload error:" + e.Message);
                    return;
                    //throw new RemoteServerErrorException("Upload Failed.", e);
                }

                SetUpdate(remoteTarget);
            });
            return job;
        }

        public override Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget,long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(GoogleDriveSystem)] " + remoteTarget.FullPath);
            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            job.DisplayName = "Download item:" + remoteTarget.ID;
            job.ProgressStr = "wait for system...";
            job.WeekDepend = WeekDepend;
            job.ForceHidden = hidden;
            JobControler.Run<Stream>(job, (j) =>
            {
                if (j != null)
                {
                    j.Progress = -1;

                    if (offset == 0)
                    {
                        // check HASH(MD5)
                        var orghash = remoteTarget.Hash;
                        orghash = (orghash.StartsWith("MD5:")) ? orghash.Substring(4) : null;
                        j.Result = Drive.DownloadItem(remoteTarget.ID, from: offset, hash: orghash, length: remoteTarget.Size, ct: j.Ct).Result;
                    }
                    else
                    {
                        j.Result = Drive.DownloadItem(remoteTarget.ID, from: offset, ct: j.Ct).Result;
                    }

                    j.Progress = 1;
                    j.ProgressStr = "ready";
                }
            });
            return job;
        }

        public override Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;
            
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItem(GoogleDriveSystem)] " + remoteTarget.Path);
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

        public override Job<IRemoteItem> DeleteItem(IRemoteItem deleteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DeleteItem(GoogleDriveSystem)] " + deleteTarget.FullPath);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Trash,
                depends: prevJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "Trash Item : " + deleteTarget.ID;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Delete] " + deleteTarget.FullPath);

                j.ProgressStr = "Delete...";
                j.Progress = -1;

                var parent = deleteTarget.Parents.First();
                try
                {
                    if(Drive.TrashItem(deleteTarget.ID, j.Ct).Result)
                    {
                        RemoveItem(deleteTarget.ID);
                    }

                    j.Result = parent;

                    j.ProgressStr = "Done";
                    j.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("Delete Failed.", e);
                }
                SetUpdate(parent);
            });
            return job;
        }

        protected override Job<IRemoteItem> MoveItemOnServer(IRemoteItem moveItem, IRemoteItem moveToItem, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[MoveItemOnServer(GoogleDriveSystem)] " + moveItem.FullPath);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: prevJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "Move item : " + moveItem.Name;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, async (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Move] " + moveItem.Name);

                j.ProgressStr = "Move...";
                j.Progress = -1;

                var oldparent = moveItem.Parents.First();
                try
                {
                    var newitem = Drive.MoveChild(moveItem.ID, oldparent.ID, moveToItem.ID, j.Ct).Result;
                    (moveItem as GoogleDriveSystemItem).SetFileds(newitem);


                    await LoadItems(moveToItem.ID, 2);
                    await LoadItems(oldparent.ID, 2);

                    j.Result = moveToItem;

                    j.ProgressStr = "Done";
                    j.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("MoveItemOnServer Failed.", e);
                }
                SetUpdate(oldparent);
                SetUpdate(moveToItem);
            });
            return job;
        }

        public override Job<IRemoteItem> RenameItem(IRemoteItem targetItem, string newName, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[RenameItem(GoogleDriveSystem)] " + targetItem.FullPath);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: prevJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "Rename item : " + targetItem.Name;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Rename] " + targetItem.Name);

                j.ProgressStr = "Rename...";
                j.Progress = -1;

                var parent = targetItem.Parents.First();
                try
                {
                    var newremoteitem = Drive.RenameItem(targetItem.ID, newName, j.Ct).Result;
                    var newitem = pathlist[newremoteitem.id] = new GoogleDriveSystemItem(this, newremoteitem, null);

                    RemoveItem(targetItem.ID);
                    (parent as GoogleDriveSystemItem).AddChild(newitem);

                    j.Result = newitem;

                    j.ProgressStr = "Done";
                    j.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("MoveItemOnServer Failed.", e);
                }
                SetUpdate(parent);
            });
            return job;
        }

        public override Job<IRemoteItem> ChangeAttribItem(IRemoteItem targetItem, IRemoteItemAttrib newAttrib, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[ChangeAttribItem(GoogleDriveSystem)] " + targetItem.FullPath);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: prevJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "ChangeAttribute item : " + targetItem.Name;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[ChangeAttribute] " + targetItem.Name);

                j.ProgressStr = "ChangeAttribute...";
                j.Progress = -1;

                var parent = targetItem.Parents.First();
                try
                {
                    var newremoteitem = Drive.FilesUpdate(targetItem.ID, new FileMetadata_Info()
                    {
                        modifiedTime_prop = newAttrib.ModifiedDate?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        createdTime_prop = newAttrib.CreatedDate?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    }, ct: j.Ct).Result;

                    (targetItem as GoogleDriveSystemItem).SetFileds(newremoteitem);
                    
                    j.ProgressStr = "Done";
                    j.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("ChangeAttribItem Failed.", e);
                }
                SetUpdate(parent);
            });
            return job;
        }
    }
}
