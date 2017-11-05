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
            if (parent == null) isRoot = true;

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
        }

        public void SetChildren(IEnumerable<IRemoteItem> children)
        {
            if (children == null)
            {
                _children.Clear();
                ChildrenIDs = new string[0];
                return;
            }
            Parallel.ForEach(children.ToDictionary(c => c.Path), (s) => {
                _children.AddOrUpdate(s.Key, (k) => s.Value, (k, v) => s.Value);
            });
            foreach (var rm in _children.Values.Except(children))
            {
                IRemoteItem t;
                _children.TryRemove(rm.Path, out t);
            }
            ChildrenIDs = Children.Select(x => x.Value.ID).ToArray();
        }

        public override void FixChain(IRemoteServer server)
        {
            _server = server;
            _parents = parentIDs?.Select(x => _server.PeakItem(FullpathToPath(x))).ToArray();
            Interlocked.CompareExchange(ref _children, new ConcurrentDictionary<string, IRemoteItem>(), null);
            if (ChildrenIDs != null)
            {
                Parallel.ForEach(ChildrenIDs.ToDictionary(k => FullpathToPath(k), v => _server.PeakItem(FullpathToPath(v))), (s) =>
                {
                    _children.AddOrUpdate(s.Key, (k) => s.Value, (k, v) => s.Value);
                });
            }
        }

        private string FullpathToPath(string fullpath)
        {
            return (IsRoot || string.IsNullOrEmpty(fullpath)) ? "" : new Uri((_server as LocalSystem).BasePath.TrimEnd('\\') + "\\").MakeRelativeUri(new Uri(fullpath)).ToString();
        }

        public override string ID => fullpath;
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
        }

        public string BasePath => localPathBase;

        public override IRemoteItem PeakItem(string path)
        {
            return pathlist[path];
        }
        protected override void EnsureItem(string path, int depth = 0)
        {
            var item = pathlist[path];
            if (item.ItemType == RemoteItemType.Folder)
                LoadItems(item.Path, depth);
            item = pathlist[path];
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
                var root = new LocalSystemItem(this, new DirectoryInfo(localPathBase), null);
                pathlist.AddOrUpdate("", (k)=>root, (k,v)=>root);
                _IsReady = true;
                TSviewCloudConfig.Config.Log.LogOut("[Add] LocalSystem {0} as {1}", localPathBase, Name);
                return true;
            }
            return false;
        }


        private LocalSystemItem updateItemChain(string key, LocalSystemItem olditem, LocalSystemItem newitem)
        {
            foreach(var p in olditem.Parents)
            {
                IRemoteItem tmp;
                p.Children.TryRemove(key, out tmp);
            }

            newitem.SetChildren(olditem.Children.Values);

            foreach(var c in olditem.Children.Values)
            {
                c.ChangeParent(olditem, newitem);
            }

            return newitem;
        }

        private void LoadItems(string path, int depth = 0)
        {
            if (depth < 0) return;
            TSviewCloudConfig.Config.Log.LogOut("[LoadItems] " + path);
            if (Directory.Exists(Path.Combine(localPathBase, Uri.UnescapeDataString(path))))
            {
                try
                {
                    var ret = new List<LocalSystemItem>();
                    var info = new DirectoryInfo(Path.Combine(localPathBase, Uri.UnescapeDataString(path)));
                    Parallel.ForEach(
                        info.EnumerateFileSystemInfos()
                            .Where(i => !(i.Attributes.HasFlag(FileAttributes.Directory) && (i.Name == "." || i.Name == ".."))),
                        () => new List<LocalSystemItem>(),
                        (x, state, local) =>
                        {
                            var item = new LocalSystemItem(this, x, pathlist[path]);
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
                    if(depth > 0)
                        Parallel.ForEach(pathlist[path].Children.Values, (x) => { LoadItems(x.Path, depth - 1); });
                }
                catch { }
            }
            else
            {
                pathlist[path].SetChildren(null);
            }
        }

        private void RemoveItem(string path)
        {
            TSviewCloudConfig.Config.Log.LogOut("[RemoveItem] " + path);
            if (pathlist.TryRemove(path, out LocalSystemItem target))
            {
                var children = target.Children.Values.ToArray();
                foreach(var child in children)
                {
                    RemoveItem(child.Path);
                }
                foreach (var p in target.Parents)
                {
                    p.Children.TryRemove(path, out IRemoteItem tmp);
                }
            }
        }

        public override Job<IRemoteItem> MakeFolder(string foldername, IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] parentJob)
        {
            if (parentJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

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

                    Directory.CreateDirectory(uploadfullpath);

                    var info = new DirectoryInfo(remoteTarget.ID);
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == uploadfullpath).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.Path, (k) => newitem, (k, v) =>
                    {
                        return updateItemChain(k, v, newitem);
                    });

                    remoteTarget.Children.AddOrUpdate(newitem.Path, newitem, (k, v) => newitem);

                    job.Result = newitem;
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

            var filesize = new FileInfo(filename).Length;
            var short_filename = Path.GetFileName(filename);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Upload,
                info: new JobControler.SubInfo
                {
                    type = JobControler.SubInfo.SubType.UploadFile,
                    size = filesize,
                },
                depends: parentJob);
            job.WeekDepend = WeekDepend;
            job.DisplayName = filename;
            job.ProgressStr = "wait for upload.";
            var ct = job.Ct;
            JobControler.Run(job, (j) =>
            {
                ct.ThrowIfCancellationRequested();
                TSviewCloudConfig.Config.Log.LogOut("[Upload] File: " + filename);

                job.ProgressStr = "Upload...";
                job.Progress = 0;

                try
                {
                    var uploadfullpath = Path.Combine(remoteTarget.ID, short_filename);

                    using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024))
                    using (var f = new PositionStream(filestream))
                    {
                        f.PosChangeEvent += (src, evnt) =>
                        {
                            if (ct.IsCancellationRequested) return;
                            var eo = evnt;
                            job.ProgressStr = eo.Log;
                            job.Progress = (double)eo.Position / eo.Length;
                            job.JobInfo.pos = eo.Position;
                        };

                        using (var destfilestream = new FileStream(uploadfullpath, FileMode.Create, FileAccess.Write, FileShare.Read, 256 * 1024))
                        {
                            f.CopyToAsync(destfilestream, TSviewCloudConfig.Config.UploadBufferSize, ct).Wait(ct);
                        }
                    }

                    job.ProgressStr = "done.";
                    job.Progress = 1;

                    var info = new DirectoryInfo(remoteTarget.ID);
                    var item = info.EnumerateFileSystemInfos().Where(x => x.FullName == uploadfullpath).FirstOrDefault();

                    var newitem = new LocalSystemItem(this, item, remoteTarget);
                    pathlist.AddOrUpdate(newitem.Path, (k) => newitem, (k, v) =>
                    {
                        return updateItemChain(k, v, newitem);
                    });

                    remoteTarget.Children.AddOrUpdate(newitem.Path, newitem, (k, v) => newitem);

                    job.Result = newitem;
                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("Upload Failed.", e);
                }

                SetUpdate(remoteTarget);
            });
            return job;
        }

        public override Job<Stream> DownloadFile(IRemoteItem remoteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            var job = JobControler.CreateNewJob<Stream>(JobClass.RemoteDownload, depends: prevJob);
            job.DisplayName = "Download item:" + remoteTarget.ID;
            job.ProgressStr = "wait for system...";
            job.WeekDepend = WeekDepend;
            JobControler.Run(job, (j) =>
            {
                job.Progress = -1;
                var stream = new LibLocalSystem.LocalFileStream(remoteTarget.ID, FileMode.Open);
                stream.MasterJob = job;
                job.Result = stream;
                job.Progress = 1;
                job.ProgressStr = "ready";
            });
            return job;
        }

        public override Job<IRemoteItem> DeleteItem(IRemoteItem deleteTarget, bool WeekDepend = false, params Job[] prevJob)
        {
            if (prevJob?.Any(x => x?.IsCanceled ?? false) ?? false) return null;

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

                var parent = deleteTarget.Parents[0];
                try
                {
                    if (Directory.Exists(deleteTarget.ID))
                    {
                        Directory.Delete(deleteTarget.ID, true);
                    }
                    else
                    {
                        File.Delete(deleteTarget.ID);
                    }

                    RemoveItem(deleteTarget.Path);

                    job.Result = parent;
                }
                catch (Exception e)
                {
                    throw new RemoteServerErrorException("Upload Failed.", e);
                }
                job.ProgressStr = "Done";
                job.Progress = 1;
                SetUpdate(parent);
            });
            return job;
        }
    }
}
