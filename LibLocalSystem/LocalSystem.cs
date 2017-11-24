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

namespace TSviewCloudPlugin
{
    [DataContract]
    public class LocalSystemItem : RemoteItemBase
    {
        [DataMember(Name = "ID")]
        private string fullpath;

        DateTime lastLoaded;

        public LocalSystemItem() : base()
        {

        }

        public LocalSystemItem(IRemoteServer server, FileSystemInfo file, params IRemoteItem[] parent) : base(server, parent)
        {
            if (!(parent?.Length > 0)) isRoot = true;

            fullpath = file?.FullName;
            fullpath = ItemControl.GetOrgFilename(fullpath);

            if (!string.IsNullOrEmpty(fullpath))
            {
                itemtype = (file.Attributes.HasFlag(FileAttributes.Directory)) ? RemoteItemType.Folder : RemoteItemType.File;
                modifiedDate = file.LastWriteTime;
                createdDate = file.CreationTime;
                accessDate = file.LastAccessTime;
                if (itemtype == RemoteItemType.File)
                {
                    try
                    {
                        var info = new FileInfo(file.FullName);
                        size = info.Length;
                    }
                    catch { }
                }
            }
            else
            {
                itemtype = RemoteItemType.Folder;
                fullpath = "";
            }

            if (isRoot) SetParent(this);
        }


        public void SetField()
        {
            if (!string.IsNullOrEmpty(fullpath))
            {
                if(itemtype == RemoteItemType.File)
                {
                    var finfo = new FileInfo(ItemControl.GetLongFilename(fullpath));
                    modifiedDate = finfo.LastWriteTime;
                    createdDate = finfo.CreationTime;
                    accessDate = finfo.LastAccessTime;
                    size = finfo.Length;
                }
                else
                {
                    var dinfo = new DirectoryInfo(ItemControl.GetLongFilename(fullpath));
                    modifiedDate = dinfo.LastWriteTime;
                    createdDate = dinfo.CreationTime;
                    accessDate = dinfo.LastAccessTime;
                }
            }
        }

        public override void FixChain(IRemoteServer server)
        {
            _server = server;
            base.FixChain(server);
        }

        private string FullpathToPath(string fullpath)
        {
            var basepath = (_server as LocalSystem).BasePath;
            if (string.IsNullOrEmpty(fullpath) || basepath == fullpath)
                return "";
            if (!fullpath.StartsWith(basepath))
                throw new ArgumentOutOfRangeException("fullpath", "out of location BaseBath");
            var ret = (basepath.EndsWith("\\"))? fullpath.Substring(basepath.Length): fullpath.Substring(basepath.Length+1);
            return ret.Replace('\\', '/');
        }

        public override string ID => fullpath;
        public override string Path => FullpathToPath(fullpath);
        public override string Name => System.IO.Path.GetFileName(ItemControl.GetLongFilename(fullpath));

        public DateTime LastLoaded { get => lastLoaded; set => lastLoaded = value; }
    }

    [DataContract]
    public class LocalSystem: RemoteServerBase
    {
        [DataMember(Name = "LocalBasePath")]
        private string localPathBase;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, LocalSystemItem> pathlist;

        private ConcurrentDictionary<string, ManualResetEventSlim> loadinglist;

        public LocalSystem()
        {
            pathlist = new ConcurrentDictionary<string, LocalSystemItem>();
            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] LocalSystem {0} as {1}", localPathBase, Name);

            loadinglist = new ConcurrentDictionary<string, ManualResetEventSlim>();
            if (pathlist == null)
            {
                pathlist = new ConcurrentDictionary<string, LocalSystemItem>();
                var root = new LocalSystemItem(this, new DirectoryInfo(localPathBase), null);
                pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            }
            else
            {
                Parallel.ForEach(
                    pathlist.Values.ToArray(), 
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => x.FixChain(this));
            }
            _IsReady = true;
        }

        public string BasePath => localPathBase;
        protected override string RootID => localPathBase;

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
                TSviewCloudConfig.Config.Log.LogOut("[EnsureItem(LocalSystem)] " + ID);
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
                TSviewCloudConfig.Config.Log.LogOut("[ReloadItem(LocalSystem)] " + ID);
                var item = pathlist[ID];
                if (item.ItemType == RemoteItemType.Folder)
                    LoadItems(ID, 1);
                item = pathlist[ID];
            }
            catch
            {
                LoadItems(ID, 1);
            }
            return PeakItem(ID);
        }

        public override void Init()
        {
            RemoteServerFactory.Register(GetServiceName(), typeof(LocalSystem));
        }

        public override string GetServiceName()
        {
            return "Local";
        }

        public override Icon GetIcon()
        {
            return LibLocalSystem.Properties.Resources.disk;
        }

        public override bool Add()
        {
            var picker = new FolderBrowserDialog();

            if(picker.ShowDialog() == DialogResult.OK)
            {
                localPathBase = picker.SelectedPath;
                var root = new LocalSystemItem(this, new DirectoryInfo(ItemControl.GetLongFilename(localPathBase)), null);
                pathlist.AddOrUpdate("", (k)=>root, (k,v)=>root);
                EnsureItem("", 1);
                _IsReady = true;
                TSviewCloudConfig.Config.Log.LogOut("[Add] LocalSystem {0} as {1}", localPathBase, Name);
                return true;
            }
            return false;
        }

        public override void ClearCache()
        {
            _IsReady = false;
            pathlist.Clear();
            var root = new LocalSystemItem(this, new DirectoryInfo(ItemControl.GetLongFilename(localPathBase)), null);
            pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            EnsureItem("", 1);
            _IsReady = true;
        }

        private void LoadItems(string ID, int depth = 0)
        {
            if (depth < 0) return;
            ID = ID ?? "";
            ID = ItemControl.GetOrgFilename(ID);

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
                    tmp.Wait();
                }
                return;
            }

            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(LocalSystem)] " + ID);
            var dirname = (string.IsNullOrEmpty(ID)) ? localPathBase : ID;
            if (!dirname.StartsWith(localPathBase))
            {
                ManualResetEventSlim tmp2;
                while (!loadinglist.TryRemove(ID, out tmp2))
                    Thread.Sleep(10);
                tmp2.Set();

                throw new ArgumentException("ID is not in localPathBase", "ID");
            }
            if (pathlist.ContainsKey(ID))
            {
                if(DateTime.Now - pathlist[ID].LastLoaded < TimeSpan.FromSeconds(30))
                {
                    ManualResetEventSlim tmp2;
                    while (!loadinglist.TryRemove(ID, out tmp2))
                        Thread.Sleep(10);
                    tmp2.Set();

                    return;
                }
            }

            if (Directory.Exists(ItemControl.GetLongFilename(dirname)))
            {
                try
                {
                    var ret = new List<LocalSystemItem>();
                    var info = new DirectoryInfo(ItemControl.GetLongFilename(dirname));
                    Parallel.ForEach(
                        info.EnumerateFileSystemInfos()
                            .Where(i => !(i.Attributes.HasFlag(FileAttributes.Directory) && (i.Name == "." || i.Name == ".."))),
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        () => new List<LocalSystemItem>(),
                        (x, state, local) =>
                        {
                            var item = new LocalSystemItem(this, x, pathlist[ID]);
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
                    pathlist[ID].LastLoaded = DateTime.Now;

                    ManualResetEventSlim tmp2;
                    while (!loadinglist.TryRemove(ID, out tmp2))
                        Thread.Sleep(10);
                    tmp2.Set();

                    if (depth > 0)
                    {
                        Parallel.ForEach(pathlist[ID].Children,
                            new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                            (x) => { LoadItems(x.ID, depth - 1); });
                    }
                }
                catch { }
            }
            else
            {
                pathlist[ID].SetChildren(null);

                ManualResetEventSlim tmp2;
                while (!loadinglist.TryRemove(ID, out tmp2))
                    Thread.Sleep(10);
                tmp2.Set();
            }
        }

        private void RemoveItem(string ID)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem] " + ID);
            if (pathlist.TryRemove(ID, out LocalSystemItem target))
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

            TSviewCloudConfig.Config.Log.LogOut("[MakeFolder(LocalSystem)] " + foldername);
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
                    var uploadfullpath = Path.Combine(remoteTarget.ID, foldername);

                    Directory.CreateDirectory(ItemControl.GetLongFilename(uploadfullpath));

                    var info = new DirectoryInfo(ItemControl.GetLongFilename(remoteTarget.ID));
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == ItemControl.GetLongFilename(uploadfullpath)).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

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

            TSviewCloudConfig.Config.Log.LogOut("[UploadStream(LocalSystem)] " + uploadname);
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
            var ct = job.Ct;
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                ct.ThrowIfCancellationRequested();

                j.ProgressStr = "Upload...";
                j.Progress = 0;

                try
                {
                    var uploadfullpath = Path.Combine(remoteTarget.ID, short_filename);
                    TSviewCloudConfig.Config.Log.LogOut("[Upload] File: " + uploadfullpath);

                    using (source)
                    using (var th = new ThrottleUploadStream(source, job.Ct))
                    using (var f = new PositionStream(th))
                    {
                        f.PosChangeEvent += (src, evnt) =>
                        {
                            if (ct.IsCancellationRequested) return;
                            var eo = evnt;
                            j.ProgressStr = eo.Log;
                            j.Progress = (double)eo.Position / eo.Length;
                            j.JobInfo.pos = eo.Position;
                        };

                        using (var destfilestream = new FileStream(ItemControl.GetLongFilename(uploadfullpath), FileMode.Create, FileAccess.Write, FileShare.Read, 256 * 1024))
                        {
                            f.CopyToAsync(destfilestream, TSviewCloudConfig.Config.UploadBufferSize, ct).Wait(ct);
                        }
                    }
                    source = null;

                    j.ProgressStr = "done.";
                    j.Progress = 1;

                    var info = new DirectoryInfo(ItemControl.GetLongFilename(remoteTarget.ID));
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == ItemControl.GetLongFilename(uploadfullpath)).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

                    j.Result = newitem;
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
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(LocalSystem)] " + remoteTarget.FullPath);
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

                    var stream = new LibLocalSystem.LocalFileStream(ItemControl.GetLongFilename(remoteTarget.ID), FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024);
                    stream.Position += offset;
                    stream.Cts = job.Cts;

                    j.Result = stream;
                    j.Progress = 1;
                    j.ProgressStr = "ready";
                }
            });
            return job;
        }

        public override Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            return DownloadItemRaw(remoteTarget, WeekDepend: WeekDepend, prevJob: prevJob);

            //TSviewCloudConfig.Config.Log.LogOut("[DownloadItem(LocalSystem)] " + remoteTarget.Path);
            //var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            //job.DisplayName = "Download item:" + remoteTarget.Name;
            //job.ProgressStr = "wait for system...";
            //job.WeekDepend = WeekDepend;
            //JobControler.Run<Stream>(job, (j) =>
            //{
            //    j.Result = new ProjectUtil.SeekableStream(remoteTarget);
            //    j.Progress = 1;
            //    j.ProgressStr = "ready";
            //});
            //return job;
        }

        public override Job<IRemoteItem> DeleteItem(IRemoteItem deleteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[DeleteItem(LocalSystem)] " + deleteTarget.FullPath);
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
                    if (Directory.Exists(ItemControl.GetLongFilename(deleteTarget.ID)))
                    {
                        Directory.Delete(ItemControl.GetLongFilename(deleteTarget.ID), true);
                    }
                    else
                    {
                        File.Delete(deleteTarget.ID);
                    }

                    RemoveItem(deleteTarget.ID);

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

            TSviewCloudConfig.Config.Log.LogOut("[MoveItemOnServer(LocalSystem)] " + moveItem.FullPath);
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
                    if(moveItem.ItemType == RemoteItemType.File)
                    {
                        File.Move(ItemControl.GetLongFilename(moveItem.ID), Path.Combine(ItemControl.GetLongFilename(moveToItem.ID), moveItem.Name));
                    }
                    else
                    {
                        Directory.Move(ItemControl.GetLongFilename(moveItem.ID),  Path.Combine(ItemControl.GetLongFilename(moveToItem.ID), moveItem.Name));
                    }

                    var info = new DirectoryInfo(ItemControl.GetLongFilename(moveToItem.ID));
                    var item = info.EnumerateFileSystemInfos().Where(x => ItemControl.GetOrgFilename(x.Name) == moveItem.Name).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, moveToItem);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    moveToItem.SetChildren(moveToItem.Children.Concat(new[] { newitem }));

                    RemoveItem(moveItem.ID);

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
                SetUpdate(oldparent);
                SetUpdate(moveToItem);
            });
            return job;
        }

        public override Job<IRemoteItem> RenameItem(IRemoteItem targetItem, string newName, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[RenameItem(LocalSystem)] " + targetItem.FullPath);
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
                    if (targetItem.ItemType == RemoteItemType.File)
                    {
                        File.Move(ItemControl.GetLongFilename(targetItem.ID), Path.Combine(Path.GetDirectoryName(ItemControl.GetLongFilename(targetItem.ID)), newName));
                    }
                    else
                    {
                        Directory.Move(ItemControl.GetLongFilename(targetItem.ID), Path.Combine(Path.GetDirectoryName(ItemControl.GetLongFilename(targetItem.ID)), newName));
                    }

                    var info = new DirectoryInfo(ItemControl.GetLongFilename(parent.ID));
                    var item = info.EnumerateFileSystemInfos().Where(x => ItemControl.GetOrgFilename(x.Name) == newName).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, parent);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    parent.SetChildren(parent.Children.Concat(new[] { newitem }));

                    RemoveItem(targetItem.ID);

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

            TSviewCloudConfig.Config.Log.LogOut("[ChangeAttribItem(LocalSystem)] " + targetItem.FullPath);
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
                    if (targetItem.ItemType == RemoteItemType.File)
                    {
                        File.SetLastWriteTime(ItemControl.GetLongFilename(targetItem.ID), newAttrib.ModifiedDate.Value);
                        File.SetCreationTime(ItemControl.GetLongFilename(targetItem.ID), newAttrib.CreatedDate.Value);
                    }
                    else
                    {
                        Directory.SetLastWriteTime(ItemControl.GetLongFilename(targetItem.ID), newAttrib.ModifiedDate.Value);
                        Directory.SetCreationTime(ItemControl.GetLongFilename(targetItem.ID), newAttrib.CreatedDate.Value);
                    }

                    (targetItem as LocalSystemItem).SetField();

                    j.Result = targetItem;
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
    }
}
