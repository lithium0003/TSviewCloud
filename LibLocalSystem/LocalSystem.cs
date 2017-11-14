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

        public LocalSystemItem() : base()
        {

        }

        public LocalSystemItem(IRemoteServer server, FileSystemInfo file, params IRemoteItem[] parent) : base(server, parent)
        {
            if (!(parent?.Length > 0)) isRoot = true;

            fullpath = file?.FullName;

            if (!string.IsNullOrEmpty(fullpath))
            {
                itemtype = (file.Attributes.HasFlag(FileAttributes.Directory)) ? RemoteItemType.Folder : RemoteItemType.File;
                modifiedDate = file.LastWriteTime;
                createdDate = file.CreationTime;
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

 
        public override void FixChain(IRemoteServer server)
        {
            _server = server;
        }

        private string FullpathToPath(string fullpath)
        {
            if(fullpath.StartsWith(@"\\?\"))
                fullpath = fullpath.Substring(4);
            return (string.IsNullOrEmpty(fullpath) || (_server as LocalSystem).BasePath == fullpath) ? "" : new Uri((_server as LocalSystem).BasePath.TrimEnd('\\') + "\\").MakeRelativeUri(new Uri(fullpath)).ToString();
        }

        public override string ID => (fullpath.StartsWith(@"\\?\"))? fullpath.Substring(4) : fullpath;
        public override string Path => FullpathToPath(fullpath);
        public override string Name => System.IO.Path.GetFileName(fullpath);
    }

    [DataContract]
    public class LocalSystem: RemoteServerBase
    {
        [DataMember(Name = "LocalBasePath")]
        private string localPathBase;
        [DataMember(Name = "Cache")]
        private ConcurrentDictionary<string, LocalSystemItem> pathlist;
        
        public LocalSystem()
        {
            pathlist = new ConcurrentDictionary<string, LocalSystemItem>();
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c)
        {
            TSviewCloudConfig.Config.Log.LogOut("[Restore] LocalSystem {0} as {1}", localPathBase, Name);
            if (pathlist == null)
            {
                pathlist = new ConcurrentDictionary<string, LocalSystemItem>();
                var root = new LocalSystemItem(this, new DirectoryInfo(localPathBase), null);
                pathlist.AddOrUpdate("", (k) => root, (k, v) => root);
            }
            else
            {
                Parallel.ForEach(pathlist.Values.ToArray(), (x) => x.FixChain(this));
            }
            _IsReady = true;
        }

        public string BasePath => localPathBase;
        protected override string RootID => localPathBase;

        public override IRemoteItem PeakItem(string ID)
        {
            if (ID == RootID) ID = RootID;
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
            if (ID == RootID) ID = RootID;
            var item = pathlist[ID];
            if (item.ItemType == RemoteItemType.Folder)
                LoadItems(ID, depth);
            item = pathlist[ID];
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
                var root = new LocalSystemItem(this, new DirectoryInfo(@"\\?\"+localPathBase), null);
                pathlist.AddOrUpdate("", (k)=>root, (k,v)=>root);
                EnsureItem("", 1);
                _IsReady = true;
                TSviewCloudConfig.Config.Log.LogOut("[Add] LocalSystem {0} as {1}", localPathBase, Name);
                return true;
            }
            return false;
        }


        private void LoadItems(string ID, int depth = 0)
        {
            if (depth < 0) return;
            ID = ID ?? "";

            TSviewCloudConfig.Config.Log.LogOut("[LoadItems(LocalSystem)] " + ID);
            var dirname = (string.IsNullOrEmpty(ID)) ? localPathBase : ID;
            if (!dirname.StartsWith(localPathBase))
                throw new ArgumentException("ID is not in localPathBase", "ID");
            if (Directory.Exists(@"\\?\" + dirname))
            {
                try
                {
                    var ret = new List<LocalSystemItem>();
                    var info = new DirectoryInfo(@"\\?\" + dirname);
                    Parallel.ForEach(
                        info.EnumerateFileSystemInfos()
                            .Where(i => !(i.Attributes.HasFlag(FileAttributes.Directory) && (i.Name == "." || i.Name == ".."))),
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
                    if(depth > 0)
                        Parallel.ForEach(pathlist[ID].Children, (x) => { LoadItems(x.ID, depth - 1); });
                }
                catch { }
            }
            else
            {
                pathlist[ID].SetChildren(null);
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
            JobControler.Run(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[MkFolder] " + foldername);

                job.ProgressStr = "Make folder...";
                job.Progress = -1;

                try
                {
                    var uploadfullpath = Path.Combine(remoteTarget.ID, foldername);

                    Directory.CreateDirectory(@"\\?\" + uploadfullpath);

                    var info = new DirectoryInfo(@"\\?\" + remoteTarget.ID);
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == uploadfullpath).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

                    job.Result = newitem;
                    job.ProgressStr = "Done";
                    job.Progress = 1;
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
            JobControler.Run(job, (j) =>
            {
                ct.ThrowIfCancellationRequested();

                job.ProgressStr = "Upload...";
                job.Progress = 0;

                try
                {
                    var uploadfullpath = Path.Combine(remoteTarget.ID, short_filename);
                    TSviewCloudConfig.Config.Log.LogOut("[Upload] File: " + uploadfullpath);

                    using (var th = new ThrottleUploadStream(source, job.Ct))
                    using (var f = new PositionStream(th))
                    {
                        f.PosChangeEvent += (src, evnt) =>
                        {
                            if (ct.IsCancellationRequested) return;
                            var eo = evnt;
                            job.ProgressStr = eo.Log;
                            job.Progress = (double)eo.Position / eo.Length;
                            job.JobInfo.pos = eo.Position;
                        };

                        using (var destfilestream = new FileStream(@"\\?\" + uploadfullpath, FileMode.Create, FileAccess.Write, FileShare.Read, 256 * 1024))
                        {
                            f.CopyToAsync(destfilestream, TSviewCloudConfig.Config.UploadBufferSize, ct).Wait(ct);
                        }
                    }

                    job.ProgressStr = "done.";
                    job.Progress = 1;

                    var info = new DirectoryInfo(@"\\?\" + remoteTarget.ID);
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == uploadfullpath).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    remoteTarget.SetChildren(remoteTarget.Children.Concat(new[] { newitem }));

                    job.Result = newitem;
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

        public override Job<IRemoteItem> UploadFile(string filename, IRemoteItem remoteTarget, string uploadname = null, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

            TSviewCloudConfig.Config.Log.LogOut("[UploadFile(LocalSystem)] " + filename);
            var filesize = new FileInfo(filename).Length;
            var short_filename = Path.GetFileName(filename);

            var filestream = new FileStream(@"\\?\" + filename, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024);
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

        public override Job<Stream> DownloadItemRaw(IRemoteItem remoteTarget,long offset = 0, bool WeekDepend = false, bool hidden = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;
            TSviewCloudConfig.Config.Log.LogOut("[DownloadItemRaw(LocalSystem)] " + remoteTarget.FullPath);
            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            job.DisplayName = "Download item:" + remoteTarget.ID;
            job.ProgressStr = "wait for system...";
            job.WeekDepend = WeekDepend;
            job.ForceHidden = hidden;
            JobControler.Run(job, (j) =>
            {
                (j as Job<Stream>).Progress = -1;

                var stream = new LibLocalSystem.LocalFileStream(remoteTarget.ID, FileMode.Open, FileAccess.Read, FileShare.Read, 256*1024);
                stream.Position += offset;

                stream.MasterJob = job;
                (j as Job<Stream>).Result = stream;
                (j as Job<Stream>).Progress = 1;
                (j as Job<Stream>).ProgressStr = "ready";
            });
            return job;
        }

        public override Job<Stream> DownloadItem(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            return DownloadItemRaw(remoteTarget, WeekDepend: WeekDepend, prevJob: prevJob);
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
            JobControler.Run(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Delete] " + deleteTarget.FullPath);

                job.ProgressStr = "Delete...";
                job.Progress = -1;

                var parent = deleteTarget.Parents.First();
                try
                {
                    if (Directory.Exists(@"\\?\" + deleteTarget.ID))
                    {
                        Directory.Delete(@"\\?\" + deleteTarget.ID, true);
                    }
                    else
                    {
                        File.Delete(deleteTarget.ID);
                    }

                    RemoveItem(deleteTarget.ID);

                    job.Result = parent;
                    job.ProgressStr = "Done";
                    job.Progress = 1;
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
            JobControler.Run(job, (j) =>
            {
                TSviewCloudConfig.Config.Log.LogOut("[Move] " + moveItem.Name);

                job.ProgressStr = "Move...";
                job.Progress = -1;

                var oldparent = moveItem.Parents.First();
                try
                {
                    if(moveToItem.ItemType == RemoteItemType.File)
                    {
                        File.Move(@"\\?\" + moveItem.ID, @"\\?\" + Path.Combine(moveToItem.ID, moveItem.Name));
                    }
                    else
                    {
                        Directory.Move(@"\\?\" + moveItem.ID, @"\\?\" + Path.Combine(moveToItem.ID, moveItem.Name));
                    }

                    var info = new DirectoryInfo(@"\\?\" + moveToItem.ID);
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == moveItem.Name).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, moveToItem);
                    pathlist.AddOrUpdate(newitem.ID, (k) => newitem, (k, v) => newitem);

                    moveToItem.SetChildren(moveToItem.Children.Concat(new[] { newitem }));

                    RemoveItem(moveItem.ID);

                    job.Result = newitem;
                    job.ProgressStr = "Done";
                    job.Progress = 1;
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("Make folder Failed.", e);
                }
                SetUpdate(oldparent);
                SetUpdate(moveToItem);
            });
            return job;
        }
    }
}
