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
using LibAmazonDrive;
using System.Net.Http;

namespace TSviewCloudPlugin
{
    [DataContract]
    public class AmazonDriveSystemItem : RemoteItemBase
    {
        [DataMember(Name = "ID")]
        private string id;
        [DataMember(Name = "Name")]
        private string name;

        DateTime lastLoaded;

        public AmazonDriveSystemItem() : base()
        {

        }

        public AmazonDriveSystemItem(IRemoteServer server, FileMetadata_Info info, params IRemoteItem[] parent) : base(server, parent)
        {
            SetFileds(info);
        }

        public void SetFileds(FileMetadata_Info info)
        {
            id = info.id;
            name = info.name;
            itemtype = (info.kind == "FOLDER") ? RemoteItemType.Folder : RemoteItemType.File;
            size = info.contentProperties?.size;
            modifiedDate = info.modifiedDate;
            createdDate = info.createdDate;
            if(info.clientProperties != null)
            {
                modifiedDate = info.clientProperties.dateUpdated;
                createdDate = info.clientProperties.dateCreated;
            }
            hash = info.contentProperties?.md5;
            if (hash != null) hash = "MD5:" + hash;
            parentIDs = info.parents;

            isRoot = info.isRoot ?? isRoot;
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
        public override string Path => (isRoot) ? "" : Parents.First().Path + ((Parents.First().Path == "") ? "" : "/") + Name;
        public override string Name => name;

        public DateTime LastLoaded { get => lastLoaded; set => lastLoaded = value; }
    }

    [DataContract]
    public class AmazonDriveSystem: RemoteServerBase
    {
        [DataMember(Name = "RootID")]
        private string rootID;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, AmazonDriveSystemItem> pathlist;

        [DataMember(Name = "RefreshToken")]
        public string _refresh_token;

        const string hidden_pass = "AmazonDrive Tokens";
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

        [DataMember(Name = "EndpointInfo")]
        private GetEndpoint_Info EndpointInfo;
        [DataMember(Name = "EndpointDate")]
        private DateTime EndpointDate;

        [DataMember(Name = "CheckPoint")]
        private string CheckPoint;
        [DataMember(Name = "LastSyncTime")]
        private DateTime LastSyncTime;


        private AmazonDrive Drive;

        public AmazonDriveSystem()
        {
            pathlist = new ConcurrentDictionary<string, AmazonDriveSystemItem>();
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] AmazonDriveSystem {0}", Name);

            Drive = new AmazonDrive()
            {
                Auth = new AuthKeys() { refresh_token = Refresh_Token },
                endpoints = EndpointInfo,
                endpoint_Age = EndpointDate,
            };
            var job = JobControler.CreateNewJob();
            job.DisplayName = "login AmazonDrive";
            job.ProgressStr = "login...";
            JobControler.Run(job, async (j) =>
            {
                job.Progress = -1;

                await Drive.EnsureToken(j.Ct);
                await Drive.EnsureEndpoint(j.Ct);

                EndpointInfo = await Drive.GetEndpoint(j.Ct);
                EndpointDate = DateTime.Now;

                job.ProgressStr = "loading cache...";
                if (pathlist == null)
                {
                    pathlist = new ConcurrentDictionary<string, AmazonDriveSystemItem>();

                    var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                    var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                    rootID = root.ID;
                    pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                }
                else
                {
                    Parallel.ForEach(
                        pathlist.Values.ToArray(),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        (x) => x.FixChain(this));
                }

                job.ProgressStr = "refresh cache...";
                ChangesLoad(j.Ct).Wait(j.Ct);

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
                return pathlist[ID];
            }
            catch
            {
                return null;
            }
        }
        protected override void EnsureItem(string ID, int depth = 0)
        {
            if (ID == "") ID = RootID;
            TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(AmazonDriveSystem)] " + ID);
            Reload(true);
        }

        public override IRemoteItem ReloadItem(string ID)
        {
            if (ID == "") ID = RootID;
            TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(AmazonDriveSystem)] " + ID);
            Reload(true);
            return PeakItem(ID);
        }

        private void Reload(bool wait = false)
        {
            if(DateTime.Now - LastSyncTime > TimeSpan.FromSeconds(15))
            {
                var job = JobControler.CreateNewJob(JobClass.LoadItem);
                job.DisplayName = "Reload AmazonDrive";
                job.ProgressStr = "loading...";
                JobControler.Run(job, (j) =>
                {
                    job.Progress = -1;
                    ChangesLoad(j.Ct).Wait(j.Ct);
                    job.Progress = 1;
                    job.ProgressStr = "done.";
                });
                job.Wait();
            }
        }

        public override void Init()
        {
            RemoteServerFactory.Register(GetServiceName(), typeof(AmazonDriveSystem));
        }

        public override string GetServiceName()
        {
            return "AmazonDrive";
        }

        public override Icon GetIcon()
        {
            return LibAmazonDrive.Properties.Resources.AmazonDrive;
        }


        private bool InitializeDrive()
        {
            var formlogin = new FormLogin(this);
            var ret = false;

            var Authkey = formlogin.Login();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "login AmazonDrive";
            job.ProgressStr = "login...";
            JobControler.Run(job, (j) =>
            {
                job.Progress = -1;
                if (Authkey != null && !string.IsNullOrEmpty(Authkey.access_token))
                {
                    job.ProgressStr = "initialize connection...";
                    Drive = new AmazonDrive()
                    {
                        Auth = Authkey,
                    };
                    Drive.EnsureToken(j.Ct).Wait(j.Ct);
                    EndpointInfo = Drive.GetEndpoint(j.Ct).Result;
                    EndpointDate = DateTime.Now;

                    ret = true;
                }
                job.Progress = 1;
                job.ProgressStr = "done.";
            });
            Cursor.Current = Cursors.WaitCursor;
            job.Wait();
            return ret;
        }

        private void AddNewDriveItem(FileMetadata_Info newdata)
        {
            AmazonDriveSystemItem value;
            if (newdata == null) return;
            if (newdata.status == "AVAILABLE")
            {
                var id = newdata.id;
                // exist item
                if (pathlist.TryGetValue(id, out value))
                {
                    value.SetFileds(newdata);
                }
                else
                {
                    if (newdata.isRoot ?? false)
                    {
                        pathlist[""].SetFileds(newdata);
                        pathlist[id] = pathlist[""];
                    }
                    else
                    {
                        pathlist[id] = new AmazonDriveSystemItem(this, newdata, null);
                    }
                }
            }
            else if (newdata.status == "TRASH" || newdata.status == "PURGED")
            {
                // deleted item
                pathlist.TryRemove(newdata.id, out value);
            }
        }

        private void ChangeDriveItem(FileMetadata_Info newdata)
        {
            AmazonDriveSystemItem value;
            if (newdata == null) return;
            if (newdata.status == "AVAILABLE")
            {
                var id = newdata.id;
                // exist item
                if (pathlist.TryGetValue(id, out value))
                {
                    if (value.Parents.Select(x => x.ID).SequenceEqual(newdata.parents))
                    {
                        value.SetFileds(newdata);
                    }
                    else
                    {
                        foreach(var p in value.Parents)
                        {
                            (p as AmazonDriveSystemItem).RemoveChild(value);
                        }
                        value.SetFileds(newdata);
                        if (!value.IsRoot)
                        {
                            foreach (var p in value.Parents)
                            {
                                (p as AmazonDriveSystemItem).AddChild(value);
                            }
                        }
                    }
                }
                else
                {
                    if (newdata.isRoot ?? false)
                    {
                        pathlist[""].SetFileds(newdata);
                        value = pathlist[id] = pathlist[""];
                    }
                    else
                    {
                        value = pathlist[id] = new AmazonDriveSystemItem(this, newdata, null);
                    }

                    if (!value.IsRoot)
                    {
                        foreach (var p in value.Parents)
                        {
                            (p as AmazonDriveSystemItem).AddChild(value);
                        }
                    }
                }
            }
            else if (newdata.status == "TRASH" || newdata.status == "PURGED")
            {
                // deleted item
                if(pathlist.TryRemove(newdata.id, out value))
                {
                    RemoveItemChain(value);
                }
            }
        }

        private void RemoveItemChain(AmazonDriveSystemItem item)
        {
            foreach (var p in item.Parents)
            {
                (p as AmazonDriveSystemItem).RemoveChild(item);
            }
            foreach (var c in item.Children)
            {
                RemoveItemChain(c as AmazonDriveSystemItem);
            }
        }

 
        private async Task ChangesLoad(CancellationToken ct = default(CancellationToken))
        {
            TSviewCloudConfig.Config.Log.LogOut("[ChangesLoad(AmazonDriveSystem)] ");
            bool init = (CheckPoint == null);
            while (!ct.IsCancellationRequested)
            {
                Changes_Info[] history = null;
                int retry = 6;
                while (--retry > 0)
                {
                    try
                    {
                        history = await Drive.Changes(checkpoint: CheckPoint, ct: ct);
                        LastSyncTime = DateTime.Now;
                        break;
                    }
                    catch (Exception ex)
                    {
                        TSviewCloudConfig.Config.Log.LogOut("[ChangesLoad(AmazonDriveSystem)] ", ex.Message);
                    }
                }
                if (history == null) break;
                foreach (var h in history)
                {
                    if (!(h.end ?? false))
                    {
                        if (h.nodes.Count() > 0)
                        {
                            if (init)
                            {
                                Parallel.ForEach(h.nodes, (item) =>
                                {
                                    ct.ThrowIfCancellationRequested();
                                    AddNewDriveItem(item);
                                });
                            }
                            else
                            {
                                foreach(var item in h.nodes)
                                {
                                    ChangeDriveItem(item);
                                }
                            }
                        }
                        CheckPoint = h.checkpoint;
                    }
                }
                if ((history.LastOrDefault()?.end ?? false))
                {
                    break;
                }
            }
            if (init)
            {
                foreach (var item in pathlist.Values.ToArray())
                {
                    if (item.IsRoot) continue;
                    ct.ThrowIfCancellationRequested();
                    foreach (var p in item.Parents.ToArray())
                    {
                        (p as AmazonDriveSystemItem).AddChild(item);
                    }
                }
            }
        }

        public override bool Add()
        {
            if(InitializeDrive())
            {
                TSviewCloudConfig.Config.Log.LogOut("[Add] AmazonDriveSystem {0}", Name);

                var job = JobControler.CreateNewJob();
                job.DisplayName = "Initialize AmazonDrive";
                job.ProgressStr = "Initialize...";
                JobControler.Run(job, (j) =>
                {
                    job.Progress = -1;

                    var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                    var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                    rootID = root.ID;
                    pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
                    
                    job.ProgressStr = "Loading tree...";
                    ChangesLoad(j.Ct).Wait(j.Ct);

                    _IsReady = true;

                    job.Progress = 1;
                    job.ProgressStr = "done.";
                });
                return true;
            }
            return false;
        }

        public override void ClearCache()
        {
            _IsReady = false;
            pathlist.Clear();

            var job = JobControler.CreateNewJob();
            job.DisplayName = "Initialize AmazonDrive";
            job.ProgressStr = "Initialize...";
            JobControler.Run(job, (j) =>
            {
                job.Progress = -1;

                var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                rootID = root.ID;
                pathlist.AddOrUpdate("", (k) => root, (k, v) => root);

                job.ProgressStr = "Loading tree...";
                ChangesLoad(j.Ct).Wait(j.Ct);

                _IsReady = true;

                job.Progress = 1;
                job.ProgressStr = "done.";
            });
        }

        private void RemoveItem(string ID)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem] " + ID);
            if (pathlist.TryRemove(ID, out AmazonDriveSystemItem target))
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

            TSviewCloudConfig.Config.Log.LogOut("[MakeFolder(AmazonDriveSystem)] " + foldername);
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
                    var newitem = pathlist[newremoteitem.id] = new AmazonDriveSystemItem(this, newremoteitem, null);

                    (remoteTarget as AmazonDriveSystemItem).AddChild(newitem);

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


        private FileMetadata_Info CheckNewfile(int check_retry, string parentID, string uploadfilename, Job<IRemoteItem> job)
        {
            FileMetadata_Info result = null;
            while (check_retry-- > 0)
            {
                try
                {
                    TSviewCloudConfig.Config.Log.LogOut("[CheckNewfile(AmazonDriveSystem)] : wait 10sec for retry..." + check_retry.ToString());
                    job.ProgressStr = "Upload : wait 10sec for retry..." + check_retry.ToString();
                    job.Progress = -1;
                    Task.Delay(TimeSpan.FromSeconds(10), job.Ct).Wait(job.Ct);

                    Drive.Changes(checkpoint: CheckPoint, ct: job.Ct).ContinueWith((t) =>
                    {
                        var children = t.Result.Where(x => (x?.nodes != null) && (!x.end ?? true)).Select(x => x.nodes).SelectMany(x => x);
                        if (children.Where(x => x.name == uploadfilename).Where(x => x.parents.First() == parentID).LastOrDefault()?.status == "AVAILABLE")
                        {
                            TSviewCloudConfig.Config.Log.LogOut("[CheckNewfile(AmazonDriveSystem)] : child found");
                            job.ProgressStr = "Upload : child found.";
                            result = children.Where(x => x.name == uploadfilename).Where(x => x.parents.First() == parentID).LastOrDefault();
                            throw new Exception("break");
                        }
                    }).Wait(job.Ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    break;
                }
            }
            return result;
        }

        const long SmallFileSize = 10 * 1024 * 1024;

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

            TSviewCloudConfig.Config.Log.LogOut("[UploadStream(AmazonDriveSystem)] " + uploadname);
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

                var error_str = "";

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

                        string uphash = null;
                        int check_retry = 0;

                        Drive.UploadStream(f, remoteTarget.ID, uploadname, streamsize, j.Ct)
                        .ContinueWith((t) =>
                        {
                            if (t.IsFaulted)
                            {
                                var e = t.Exception;
                                e.Flatten().Handle(ex =>
                                {
                                    if (ex is AmazonDriveUploadException)
                                    {
                                        uphash = ex.Message;
                                        if (ex.InnerException is HttpRequestException)
                                        {
                                            if (ex.InnerException.Message.Contains("408")) check_retry = 6 * 5 + 1;
                                            if (ex.InnerException.Message.Contains("409")) check_retry = 3;
                                            if (ex.InnerException.Message.Contains("504")) check_retry = 6 * 5 + 1;
                                            if (filesize < SmallFileSize) check_retry = 3;
                                            error_str += ex.InnerException.Message + "\n";
                                        }
                                    }
                                    else
                                    {
                                        error_str += ex.Message + "\n";
                                        check_retry = 0;
                                    }
                                    return true;
                                });
                                e.Handle(ex =>
                                {
                                    return true;
                                });
                                return;
                            }
                            if (t.IsCanceled) return;

                            newremoteitem = t.Result;
                        }).Wait(j.Ct);

                        if (newremoteitem != null)
                        {
                            j.ProgressStr = "Upload done.";
                            j.Progress = -1;
                        }
                        else
                        {
                            j.ProgressStr = "Upload probably failed... checking new item.";
                            j.Progress = double.NaN;

                            newremoteitem = CheckNewfile(check_retry, remoteTarget.ID, uploadname, j);
                        }

                        if(newremoteitem == null)
                        {
                            j.ProgressStr = "Upload failed.";
                            j.Progress = double.NaN;
                        }
                        else
                        {
                            j.Progress = 1;
                            var newitem = pathlist[newremoteitem.id] = new AmazonDriveSystemItem(this, newremoteitem, null);
                            (remoteTarget as AmazonDriveSystemItem).AddChild(newitem);

                            if (uphash != null && uphash != newremoteitem.contentProperties.md5)
                            {
                                j.ProgressStr = "Upload failed. Hash not match";
                                j.Progress = double.NaN;
                                newitem.IsBroken = true;
                            }

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
                    throw new RemoteServerErrorException("Upload Failed.", e);
                }

                SetUpdate(remoteTarget);
            });
            return job;
        }

        public override Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget,long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(AmazonDriveSystem)] " + remoteTarget.FullPath);
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
            
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItem(AmazonDriveSystem)] " + remoteTarget.Path);
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

            TSviewCloudConfig.Config.Log.LogOut("[DeleteItem(AmazonDriveSystem)] " + deleteTarget.FullPath);
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

            TSviewCloudConfig.Config.Log.LogOut("[MoveItemOnServer(AmazonDriveSystem)] " + moveItem.FullPath);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.RemoteOperation,
                depends: prevJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = "Move item : " + moveItem.Name;
            job.ProgressStr = "wait for operation.";
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Move] " + moveItem.Name);

                j.ProgressStr = "Move...";
                j.Progress = -1;

                var oldparent = moveItem.Parents.First();
                try
                {
                    if(Drive.MoveChild(moveItem.ID, oldparent.ID, moveToItem.ID, j.Ct).Result)
                    {
                        ChangesLoad(j.Ct).Wait(j.Ct);
                    }

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

            TSviewCloudConfig.Config.Log.LogOut("[RenameItem(AmazonDriveSystem)] " + targetItem.FullPath);
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
                    var newitem = pathlist[newremoteitem.id] = new AmazonDriveSystemItem(this, newremoteitem, null);

                    RemoveItem(targetItem.ID);
                    (parent as AmazonDriveSystemItem).AddChild(newitem);

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

            TSviewCloudConfig.Config.Log.LogOut("[ChangeAttribItem(AmazonDriveSystem)] " + targetItem.FullPath);
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
                    var attr = new RemoteItemAttrib(newAttrib.ModifiedDate ?? targetItem.ModifiedDate, newAttrib.CreatedDate ?? targetItem.CreatedDate);
                    var newremoteitem = Drive.SetFileMetadata(targetItem.ID, attr, j.Ct).Result;

                    (targetItem as AmazonDriveSystemItem).SetFileds(newremoteitem);
                    
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
