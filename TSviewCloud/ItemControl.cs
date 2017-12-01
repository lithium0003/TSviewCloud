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

    }
}
