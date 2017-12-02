using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSviewCloudConfig;

namespace TSviewCloudPlugin
{
    public class ItemControl
    {
        internal static SynchronizationContext synchronizationContext;

        static ConcurrentDictionary<string, int> _ReloadRequest = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, int> ReloadRequest { get => _ReloadRequest; set => _ReloadRequest = value; }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static public string GetLongFilename(string filename)
        {
            if (filename.StartsWith(@"\\")) return filename;
            return @"\\?\" + filename;
        } 
            

        static public string GetOrgFilename(string Longfilename)
        {
            if (Longfilename.StartsWith(@"\\?\")) return Longfilename.Substring(4);
            return Longfilename;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static public string GetCommonPath(IEnumerable<IRemoteItem> items)
        {
            string common = null;
            foreach(var item in items)
            {
                if(common == null)
                {
                    common = item.FullPath;
                    continue;
                }
                while (!item.FullPath.StartsWith(common) && common != "")
                {
                    if (common.EndsWith("://"))
                    {
                        return "";
                    }
                    else
                    {
                        var ind = common.LastIndexOf('/');
                        common = common.Substring(0, ind);
                    }
                }
            }
            return common;
        }

        static public string GetLocalFullPath(string fullpath, string commonpath = "")
        {
            var ret = new List<string>();

            if (fullpath.StartsWith(commonpath))
            {
                if (fullpath == commonpath) return "";
                fullpath = fullpath.Substring(commonpath.Length);
            }

            var m = Regex.Match(fullpath, @"^(?<server>[^:]+)(://)(?<path>.*)$");
            if (m.Success)
            {
                var servername = m.Groups["server"].Value;
                fullpath = m.Groups["path"].Value;
                ret.Add(servername);
            }

            while (!string.IsNullOrEmpty(fullpath))
            {
                m = Regex.Match(fullpath, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                fullpath = m.Groups["next"].Value;

                var itemname = m.Groups["current"].Value;
                ret.Add(itemname);
            }
            return string.Join("\\", ret);
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static public async Task MakeSureItem(IRemoteItem item)
        {
            item = await RemoteServerFactory.PathToItem(item.FullPath);
            if (item.Children?.Count() != 0)
            {
                foreach (var c in item.Children)
                {
                    await MakeSureItem(c);
                }
            }
        }

        static private Job[] __DoDownloadFolder(string localfoldername, IEnumerable<IRemoteItem> remoteItems, Job prevJob = null)
        {
            var ret = new List<Job>();
            foreach (var item in remoteItems)
            {
                if (item.ItemType == RemoteItemType.File)
                {
                    prevJob = DownloadFile(Path.Combine(localfoldername, item.Name), item, prevJob: prevJob, weekdepend: true);
                    ret.Add(prevJob);
                }
                else
                {
                    var dname = Path.Combine(localfoldername, item.Name);
                    Directory.CreateDirectory(GetLongFilename(dname));
                    ret.AddRange(__DoDownloadFolder(dname, item.Children, prevJob));
                }
            }
            return ret.ToArray();
        }

        static private Job DoDownloadFolder(string localfoldername, IEnumerable<IRemoteItem> remoteItems, Job prevJob = null)
        {
            var job = JobControler.CreateNewJob(JobClass.ControlMaster, depends: prevJob);
            job.DisplayName = "Download items";
            JobControler.Run(job, (j) =>
            {
                Task.WaitAll(__DoDownloadFolder(localfoldername, remoteItems, j).Select(x => x.WaitTask(ct: j.Ct)).ToArray(), j.Ct);
            });
            return job;
        }

        static public Job DownloadFile(string localfilename,  IRemoteItem remoteItem, Job prevJob = null, bool weekdepend = false)
        {
            Config.Log.LogOut("Download : " + remoteItem.Name);

            Directory.CreateDirectory(Path.GetDirectoryName(GetLongFilename(localfilename)));
            if (Path.GetFileName(GetLongFilename(localfilename)) == "")
                localfilename += remoteItem.Name;

            var download = remoteItem.DownloadItemRawJob(prevJob: prevJob, WeekDepend: weekdepend);
 
            var job = JobControler.CreateNewJob<Stream>(
                 type: JobClass.Download,
                 info: new JobControler.SubInfo
                 {
                     type = JobControler.SubInfo.SubType.DownloadFile,
                     size = remoteItem?.Size ?? 0,
                 },
                 depends: download);
            job.DisplayName = remoteItem.Name;
            job.ProgressStr = "Wait for download";
            job.WeekDepend = false;

            JobControler.Run<Stream>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if(result.TryGetTarget(out var remotestream))
                {
                    using (remotestream)
                    {
                        FileStream outfile = null;
                        if (Config.DownloadConflictBehavior == DownloadBehavior.OverrideAlways)
                        {
                            outfile = new FileStream(GetLongFilename(localfilename), FileMode.Create);
                        }
                        else
                        {
                            try
                            {
                                outfile = new FileStream(GetLongFilename(localfilename), FileMode.CreateNew);
                            }
                            catch (IOException)
                            {
                                if (Config.DownloadConflictBehavior == DownloadBehavior.Prompt)
                                {
                                    synchronizationContext.Send((o) =>
                                    {
                                        var ans = MessageBox.Show("Override file? " + localfilename, "File already exists", MessageBoxButtons.YesNoCancel);
                                        if (ans == DialogResult.Cancel)
                                            throw new OperationCanceledException("User cancel");
                                        if (ans == DialogResult.No)
                                            j.Cancel();
                                    }, null);

                                    if (j.IsCanceled) return;
                                    outfile = new FileStream(GetLongFilename(localfilename), FileMode.Create);
                                }
                                else
                                {
                                    TSviewCloud.FormConflicts.Instance.AddResult(remoteItem.FullPath, "Skip download");
                                    return;
                                }
                            }
                        }
                        using (outfile)
                        {
                            try
                            {
                                using (var th = new ThrottleDownloadStream(remotestream, job.Ct))
                                using (var f = new PositionStream(th, remoteItem.Size ?? 0))
                                {
                                    f.PosChangeEvent += (src, evnt) =>
                                    {
                                        j.Progress = (double)evnt.Position / evnt.Length;
                                        j.ProgressStr = evnt.Log;
                                        j.JobInfo.pos = evnt.Position;
                                    };
                                    j.Ct.ThrowIfCancellationRequested();
                                    f.CopyToAsync(outfile, Config.DownloadBufferSize, j.Ct).Wait(j.Ct);
                                }
                                Config.Log.LogOut("Download : Done");
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Config.Log.LogOut("Download : Error " + ex.ToString());
                                JobControler.ErrorOut("Download : Error {0}\n{1}", remoteItem.Name, ex.ToString());
                                j.ProgressStr = "Error detected.";
                                j.Progress = double.NaN;
                            }
                        }
                    }
                }

                job.ProgressStr = "done.";
                job.Progress = 1;
            });
            return job;
        }


        static public Job DownloadFolder(string localfoldername, IEnumerable<IRemoteItem> remoteItems, Job prevJob = null, bool weekdepend = false)
        {
            var items = remoteItems.ToArray();

            var job = JobControler.CreateNewJob(JobClass.LoadItem, depends: prevJob);
            job.DisplayName = "Search Items";
            job.WeekDepend = weekdepend;
            JobControler.Run(job, async (j) =>
            {
                foreach (var item in items)
                {
                    await MakeSureItem(item);
                }
            });
            return DoDownloadFolder(localfoldername, items, job);
        }


        static public void DownloadItems(IEnumerable<IRemoteItem> remoteItems)
        {
            if (remoteItems == null || remoteItems.Count() == 0) return;
            if(remoteItems.Count() == 1)
            {
                var item = remoteItems.First();
                var dialog = new SaveFileDialog();
                dialog.FileName = item.Name;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                DownloadFile(dialog.FileName, item);
            }
            else
            {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() != DialogResult.OK) return;

                var remotebasepath = TSviewCloud.FormMatch.GetBasePathRemote(remoteItems.Select(x => x.FullPath));
                foreach(var item in remoteItems)
                {
                    var path = string.Join("\\", item.FullPath.Substring(remotebasepath.Length).Replace("://", "/").Split('/').Select(x => Uri.UnescapeDataString(x)));
                    DownloadFile(Path.Combine(GetLocalFullPath(dialog.SelectedPath), path), item);
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        static public IEnumerable<Job<IRemoteItem>> UploadFiles(IRemoteItem targetItem, IEnumerable<string> uploadFilenames, bool WeekDepend = false, params Job[] parentJob)
        {
            var joblist = new List<Job<IRemoteItem>>();
            if (uploadFilenames == null) return joblist;

            foreach(var upfile in uploadFilenames)
            {
                var job = targetItem.UploadFile(upfile, WeekDepend: WeekDepend, parentJob: parentJob);
                if (job == null) continue;
                job.DisplayName = string.Format("Upload File {0} to {1}", upfile, targetItem.FullPath);
                joblist.Add(job);
            }
            return joblist;
        }

        static public Job<IRemoteItem> UploadFolder(IRemoteItem targetItem, string uploadFolderName, bool WeekDepend = false, Job prevJob = null)
        {
            var mkfolder = targetItem.MakeFolder(Path.GetFileName(uploadFolderName), WeekDepend, prevJob);
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Upload,
                info: new JobControler.SubInfo
                {
                    type = JobControler.SubInfo.SubType.UploadDirectory,
                },
                depends: mkfolder);
            job.DisplayName = string.Format("Upload Folder {0} to {1}", uploadFolderName, targetItem.FullPath);
            JobControler.Run<IRemoteItem>(job, (j) =>
            {
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var folder))
                {
                    j.Result = folder;

                    j.Progress = -1;
                    j.ProgressStr = "upload...";

                    var joblist = new List<Job<IRemoteItem>>();
                    joblist.AddRange(Directory.EnumerateDirectories(GetLongFilename(uploadFolderName)).Select(x => UploadFolder(folder, GetOrgFilename(x), true, j)));
                    joblist.AddRange(UploadFiles(folder, Directory.EnumerateFiles(GetLongFilename(uploadFolderName)), true, j));
                }
                j.ProgressStr = "done";
                j.Progress = 1;
            });

            return job;
        }


        static public string GetBasePath(IEnumerable<string> paths)
        {
            string prefix = null;
            foreach (var p in paths)
            {
                if (prefix == null)
                {
                    var filename = Path.GetFileName(p);
                    prefix = p.Substring(0, p.Length - filename.Length);
                }
                if (prefix == "")
                    break;
                while (!p.StartsWith(prefix) && prefix != "")
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                    var filename = Path.GetFileName(prefix);
                    prefix = prefix.Substring(0, prefix.Length - filename.Length);
                }
            }
            return prefix ?? "";
        }

        static private IEnumerable<string> Splitpath(string path)
        {
            return path.Replace(":\\", "\\").Split('\\');
        }

        static public Job UploadFilesMultiFolder(IRemoteItem targetItem, IEnumerable<string> uploadFilenames, bool WeekDepend = false, params Job[] parentJob)
        {
            if (uploadFilenames == null) return null;

            var localbase = GetBasePath(uploadFilenames);
            var upitems = uploadFilenames.Select(x => (path: Splitpath(x.Substring(localbase.Length)), file: x));
            var makepaths = upitems.Select(x => string.Join("/", x.path.Reverse().Skip(1).Reverse())).GroupBy(x => x).Select(x => x.Key);

            var job = JobControler.CreateNewJob(JobClass.LoadItem, depends: parentJob);
            job.DisplayName = "Search Items";
            job.WeekDepend = WeekDepend;
            JobControler.Run(job, async (j) =>
            {
                await MakeSureItem(targetItem);

                foreach (var folders in makepaths)
                {
                    var current = targetItem;
                    foreach(var p in folders.Split('/'))
                    {
                        if (string.IsNullOrEmpty(p)) continue;

                        var c = current.Children.FirstOrDefault(x => x.Name == p);
                        if(c == null)
                        {
                            var mkjob = current.MakeFolder(p, true, j);
                            mkjob.Wait(ct: j.Ct);
                            c = mkjob.Result;
                        }
                        current = c;
                    }
                }

                foreach (var item in upitems)
                {
                    var current = targetItem;
                    foreach(var p in item.path.Reverse().Skip(1).Reverse())
                    {
                        current = current?.Children.FirstOrDefault(x => x.Name == p);
                    }
                    if(current == null)
                    {
                        TSviewCloud.FormConflicts.Instance.AddResult(item.file, "upload error: folder lost");
                        continue;
                    }
                    var upjob = current.UploadFile(item.file, WeekDepend: true, parentJob: j);
                    if (upjob == null) continue;
                    upjob.DisplayName = string.Format("Upload File {0} to {1}", item.file, current.FullPath);
                }
            });
            return job;
        }
    }
}
