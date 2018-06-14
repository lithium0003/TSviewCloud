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
        }

        public void AddChild(IRemoteItem newchild)
        {
            ChildrenIDs = ChildrenIDs?.Concat(new[] { newchild.ID }).ToArray() ?? new[] { newchild.ID };
            ChildrenIDs = ChildrenIDs.Distinct().ToArray();
        }

        public void RemoveChild(IRemoteItem child)
        {
            ChildrenIDs = ChildrenIDs?.Except(new[] { child.ID }).ToArray() ?? new[] { child.ID };
        }
        public void RemoveChild(string child)
        {
            ChildrenIDs = ChildrenIDs?.Except(new[] { child }).ToArray() ?? new[] { child };
        }

        public override void FixChain(IRemoteServer server)
        {
            _server = server;
            base.FixChain(server);
        }

        public override string ID => id;
        public override string Path => (isRoot) ? "" : Parents.First().Path + ((Parents.First().Path == "") ? "" : "/") + PathItemName;
        public override string Name => name;
    }

    [DataContract]
    public class AmazonDriveSystem: RemoteServerBase
    {
        [DataMember(Name = "RootID")]
        private string rootID;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, AmazonDriveSystemItem> pathlist;

        private ConcurrentDictionary<string, ManualResetEventSlim> loadinglist;

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


        private AmazonDrive Drive;

        public AmazonDriveSystem()
        {
            pathlist = new ConcurrentDictionary<string, AmazonDriveSystemItem>();
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] AmazonDriveSystem {0}", Name);

            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
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

                await Drive.EnsureToken(j.Ct).ConfigureAwait(false);
                await Drive.EnsureEndpoint(j.Ct).ConfigureAwait(false);

                EndpointInfo = await Drive.GetEndpoint(j.Ct).ConfigureAwait(false);
                EndpointDate = DateTime.Now;

                job.ProgressStr = "loading cache...";
                if (pathlist == null)
                {
                    pathlist = new ConcurrentDictionary<string, AmazonDriveSystemItem>();

                    var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                    var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                    rootID = root.ID;
                    pathlist[""] = pathlist[rootID] = root;

                    await LoadItems(rootID, 1).ConfigureAwait(false);
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
            TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(AmazonDriveSystem)] " + ID);
            try
            {
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    await LoadItems(ID, depth).ConfigureAwait(false);
                item = pathlist[ID];
            }
            catch
            {
                await LoadItems(ID, depth).ConfigureAwait(false);
            }
        }

        public async override Task<IRemoteItem> ReloadItem(string ID)
        {
            if (ID == "") ID = RootID;
            TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(AmazonDriveSystem)] " + ID);
            try
            {
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    await LoadItems(ID, 2).ConfigureAwait(false);
                item = pathlist[ID];
            }
            catch
            {
                await LoadItems(ID, 2).ConfigureAwait(false);
            }
            return PeakItem(ID);
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


        private async Task<bool> InitializeDrive()
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
            await job.WaitTask().ConfigureAwait(false);
            return ret;
        }


        private async Task LoadItems(string ID, int depth = 0)
        {
            if (depth < 0) return;
            ID = ID ?? RootID;

            if (pathlist[ID].ItemType == RemoteItemType.File) return;

            if (DateTime.Now - pathlist[ID].LastLoaded < TimeSpan.FromSeconds(60))
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
                    await Task.Run(() => tmp.Wait()).ConfigureAwait(false);
                }
                return;
            }

            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(AmazonDriveSystem)] " + ID);

            try
            {
                var job = JobControler.CreateNewJob();
                job.DisplayName = "Loading AmazonDrive Item:" + ID;
                job.ProgressStr = "Initialize...";
                JobControler.Run(job, (j) =>
                {
                    job.Progress = -1;

                    job.ProgressStr = "Loading children...";

                    var me = Drive.GetFileMetadata(ID, ct: j.Ct).Result;
                    if (me == null || (me.status != "AVAILABLE"))
                    {
                        RemoveItem(ID);
                        return;
                    }
                    pathlist[ID].SetFileds(me);

                    var children = Drive.ListChildren(ID, ct: j.Ct).Result;
                    if (children != null)
                    {
                        var ret = new List<AmazonDriveSystemItem>();
                        Parallel.ForEach(
                            children.data,
                            new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                            () => new List<AmazonDriveSystemItem>(),
                            (x, state, local) =>
                            {
                                if (x.status != "AVAILABLE") return local;
                                var item = new AmazonDriveSystemItem(this, x, pathlist[ID]);
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
                await job.WaitTask().ConfigureAwait(false);
            }
            catch
            {
                RemoveItem(ID);
            }
            finally
            {
                ManualResetEventSlim tmp3;
                while (!loadinglist.TryRemove(ID, out tmp3))
                    await Task.Delay(10).ConfigureAwait(false);
                tmp3.Set();
            }

            if (depth > 0)
            {
                Parallel.ForEach(pathlist[ID].Children.Where(x => x.ItemType == RemoteItemType.Folder),
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => { LoadItems(x.ID, depth - 1).ConfigureAwait(false); });
            }
        }

        public async override Task<bool> Add()
        {
            if(await InitializeDrive().ConfigureAwait(false))
            {
                TSviewCloudConfig.Config.Log.LogOut("[Add] AmazonDriveSystem {0}", Name);

                var job = JobControler.CreateNewJob();
                job.DisplayName = "Initialize AmazonDrive";
                job.ProgressStr = "Initialize...";
                JobControler.Run(job, async (j) =>
                {
                    job.Progress = -1;

                    var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                    var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                    rootID = root.ID;
                    pathlist[""] = pathlist[rootID] = root;

                    await LoadItems(rootID, 1).ConfigureAwait(false);

                    _IsReady = true;

                    j.Progress = 1;
                    j.ProgressStr = "done.";


                    var job2 = JobControler.CreateNewJob();
                    job2.DisplayName = "Scan AmazonDrive";
                    job2.ProgressStr = "Scan...";
                    JobControler.Run(job2, async (j2) =>
                    {
                        j2.Progress = -1;

                        await ScanItems(pathlist[rootID], j2.Ct).ConfigureAwait(false);

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
            await LoadItems(baseitem.ID, 0).ConfigureAwait(false);
            foreach (var i in baseitem.Children)
            {
                ct.ThrowIfCancellationRequested();
                if (i.ItemType == RemoteItemType.Folder)
                    await ScanItems(i, ct).ConfigureAwait(false);
            }
        }

        public override void ClearCache()
        {
            _IsReady = false;
            pathlist.Clear();
            
            var job = JobControler.CreateNewJob();
            job.DisplayName = "Initialize AmazonDrive";
            job.ProgressStr = "Initialize...";
            JobControler.Run(job, async (j) =>
            {
                job.Progress = -1;

                var rootitme = Drive.ListMetadata(filters: "isRoot:true", ct: j.Ct).Result;

                var root = new AmazonDriveSystemItem(this, rootitme.data[0], null);
                rootID = root.ID;
                pathlist[""] = pathlist[rootID] = root;

                await LoadItems(rootID, 1).ConfigureAwait(false);

                _IsReady = true;

                job.Progress = 1;
                job.ProgressStr = "done.";

                var job2 = JobControler.CreateNewJob();
                job2.DisplayName = "Scan AmazonDrive";
                job2.ProgressStr = "Scan...";
                JobControler.Run(job2, async (j2) =>
                {
                    j2.Progress = -1;

                    await ScanItems(pathlist[rootID], j2.Ct).ConfigureAwait(false);

                    j2.Progress = 1;
                    j2.ProgressStr = "done.";
                });
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

                    Drive.ListChildren(parentID, ct: job.Ct).ContinueWith((t) =>
                    {
                        var children = t.Result.data;
                        if (children.Where(x => x.name == uploadfilename).LastOrDefault()?.status == "AVAILABLE")
                        {
                            TSviewCloudConfig.Config.Log.LogOut("[CheckNewfile(AmazonDriveSystem)] : child found");
                            job.ProgressStr = "Upload : child found.";
                            result = children.Where(x => x.name == uploadfilename).LastOrDefault();
                            throw new Exception("break");
                        }
                    }).Wait(job.Ct);
                    //Drive.Changes(checkpoint: CheckPoint, ct: job.Ct).ContinueWith((t) =>
                    //{
                    //    var children = t.Result.Where(x => (x?.nodes != null) && (!x.end ?? true)).Select(x => x.nodes).SelectMany(x => x);
                    //    if (children.Where(x => x.name == uploadfilename).Where(x => x.parents.First() == parentID).LastOrDefault()?.status == "AVAILABLE")
                    //    {
                    //        TSviewCloudConfig.Config.Log.LogOut("[CheckNewfile(AmazonDriveSystem)] : child found");
                    //        job.ProgressStr = "Upload : child found.";
                    //        result = children.Where(x => x.name == uploadfilename).Where(x => x.parents.First() == parentID).LastOrDefault();
                    //        throw new Exception("break");
                    //    }
                    //}).Wait(job.Ct);
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
                    using (var f = new PositionStream(th, streamsize))
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
                            LogFailed(remoteTarget.FullPath + "/" + uploadname, "upload error");
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
                                LogFailed(newitem.FullPath, "upload error: hash failed");
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
            JobControler.Run<IRemoteItem>(job, async (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Move] " + moveItem.Name);

                j.ProgressStr = "Move...";
                j.Progress = -1;

                var oldparent = moveItem.Parents.First();
                try
                {
                    var newitem = Drive.MoveChild(moveItem.ID, oldparent.ID, moveToItem.ID, j.Ct).Result;
                    (moveItem as AmazonDriveSystemItem).SetFileds(newitem);

                    await LoadItems(moveToItem.ID, 2).ConfigureAwait(false);
                    await LoadItems(oldparent.ID, 2).ConfigureAwait(false);

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
