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
    class ItemControl
    {
        internal static SynchronizationContext synchronizationContext;

        static ConcurrentDictionary<string, int> _ReloadRequest = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, int> ReloadRequest { get => _ReloadRequest; set => _ReloadRequest = value; }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        static private void MakeSureItem(IRemoteItem item)
        {
            item = RemoteServerFactory.PathToItem(item.FullPath);
            if (item.Children?.Count() != 0)
            {
                foreach (var c in item.Children)
                {
                    MakeSureItem(c);
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
                    Directory.CreateDirectory(dname);
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
                Task.WaitAll(__DoDownloadFolder(localfoldername, remoteItems, job).Select(x => x.WaitTask(ct: job.Ct)).ToArray(), job.Ct);
            });
            return job;
        }

        static public Job DownloadFile(string localfilename,  IRemoteItem remoteItem, Job prevJob = null, bool weekdepend = false)
        {
            Config.Log.LogOut("Download : " + remoteItem.Name);

            var download = remoteItem.DownloadItemJob(prevJob: prevJob);
            download.WeekDepend = weekdepend;

            var job = JobControler.CreateNewJob(
                 type: JobClass.Download,
                 info: new JobControler.SubInfo
                 {
                     type = JobControler.SubInfo.SubType.DownloadFile,
                     size = remoteItem?.Size ?? 0,
                 },
                 depends: download);
            job.DisplayName = remoteItem.Name;
            job.ProgressStr = "Wait for download";

            JobControler.Run(job, (j) =>
            {
                using (var remotestream = job.resultOfDepend[0] as Stream)
                {
                    FileStream outfile;
                    try
                    {
                        outfile = new FileStream(localfilename, FileMode.CreateNew);
                    }
                    catch (IOException)
                    {
                        synchronizationContext.Send((o) =>
                        {
                            var ans = MessageBox.Show("Override file? " + localfilename, "File already exists", MessageBoxButtons.YesNoCancel);
                            if (ans == DialogResult.Cancel)
                                throw new OperationCanceledException("User cancel");
                            if (ans == DialogResult.No)
                                job.Cancel();
                        }, null);

                        if (job.IsCanceled) return;
                        outfile = new FileStream(localfilename, FileMode.Create);
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
                                    job.Progress = (double)evnt.Position / evnt.Length;
                                    job.ProgressStr = evnt.Log;
                                    job.JobInfo.pos = evnt.Position;
                                };
                                job.Ct.ThrowIfCancellationRequested();
                                f.CopyToAsync(outfile, Config.DownloadBufferSize, job.Ct).Wait(job.Ct);
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
                            job.ProgressStr = "Error detected.";
                            job.Progress = double.NaN;
                        }
                    }
                }

                job.ProgressStr = "done.";
                job.Progress = 1;
            });
            return job;
        }


        static public Job DownloadFolder(string localfoldername, IEnumerable<IRemoteItem> remoteItems, Job prevJob = null)
        {
            var items = remoteItems.ToArray();

            var job = JobControler.CreateNewJob(JobClass.LoadItem, depends: prevJob);
            job.DisplayName = "Search Items";
            JobControler.Run(job, (j) =>
            {
                foreach (var item in items)
                {
                    MakeSureItem(item);
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
                joblist.Add(targetItem.UploadFile(upfile, WeekDepend: WeekDepend, parentJob: parentJob));
            }
            return joblist;
        }

        static public Job<IRemoteItem> UploadFolder(IRemoteItem targetItem, string uploadFolderName, bool WeekDepend = false, Job prevJob = null)
        {
            var job = JobControler.CreateNewJob<IRemoteItem>(
                type: JobClass.Upload,
                info: new JobControler.SubInfo
                {
                    type = JobControler.SubInfo.SubType.UploadDirectory,
                },
                depends: targetItem.MakeFolder(Path.GetFileName(uploadFolderName), WeekDepend, prevJob));
            job.DisplayName = string.Format("Upload Folder {0} to {1}", uploadFolderName, targetItem.FullPath);
            JobControler.Run(job, (j) =>
            {
                var folder = job.ResultOfDepend[0];
                job.Result = folder;

                job.Progress = -1;
                job.ProgressStr = "upload...";

                var joblist = new List<Job<IRemoteItem>>();
                joblist.AddRange(Directory.EnumerateDirectories(uploadFolderName).Select(x => UploadFolder(folder, x, true, job)));
                joblist.AddRange(UploadFiles(folder, Directory.EnumerateFiles(uploadFolderName), true, job));

                //Parallel.ForEach(joblist, (x)=>x.Wait(ct: job.Ct));
                job.ProgressStr = "done";
                job.Progress = 1;
            });

            return job;
        }

    }
}
