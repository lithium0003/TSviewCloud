using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloudPlugin
{
    public sealed partial class FormTaskList : Form
    {
        private FormTaskList()
        {
            InitializeComponent();
        }

        private static readonly FormTaskList _instance = new FormTaskList();

        public static FormTaskList Instance
        {
            get
            {
                return _instance;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            e.Cancel = true;
            Hide();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Start();
        }

        public void Display()
        {
            timer2.Enabled = true;
        }

        public void Start()
        {
            timer1.Enabled = true;
        }

        public void Stop()
        {
            timer1.Enabled = false;
            timer2.Enabled = false;
            Hide();
            TSviewCloud.FormClosing.Instance.Close();
        }

        int tic = 0;
        Job[] internalJobList;


        private void timer1_Tick(object sender, EventArgs e)
        {
            tic++;
            if (tic > 10000) tic = 0;
            listView1.Invalidate();
            if (tic % 10 != 0) return;

            var joblist = JobControler.JobList();
            if (joblist == null) return;
            var now = DateTime.Now;
            internalJobList = joblist.Where(x => !x.IsHidden)
                .OrderBy(x => (x.IsInfo)? 0: (x.IsDone) ? 1 : (x.IsRunning) ? 2 : 3)
                .ThenBy(x => x.Index)
                .ThenBy(x => (x.StartTime == default(DateTime)) ? now : x.StartTime)
                .ThenBy(x => x.QueueTime).ToArray();
            listView1.VirtualListSize = internalJobList.Length;
            if(listView1.VirtualListSize > 0)
            {
                var str = new StringBuilder();
                str.AppendFormat("All:{0} ", internalJobList.Length);
                int d = JobControler.JobTypeCount(JobClass.Download);
                int u = JobControler.JobTypeCount(JobClass.Upload);
                int p = JobControler.JobTypeCount(JobClass.PlayDownload);
                int o = internalJobList.Length - (d + u + p);
                if (d > 0)
                    str.AppendFormat("Download:{0} ", d);
                if (u > 0)
                    str.AppendFormat("Upload:{0} ", u);
                if (p > 0)
                    str.AppendFormat("Play:{0} ", p);
                if (o > 0)
                    str.AppendFormat("Other:{0} ", o);
                label1.Text = str.ToString();
            }
            else
            {
                label1.Text = "";
            }
            if (internalJobList.Length == 0)
                Stop();
        }

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        ListViewItem[] runingItems;
        int firstItem;

        private ListViewItem GetListViewItem(int index)
        {
            var jobs = internalJobList;
            if (jobs == null) return new ListViewItem();
            if (jobs.Length <= index) return new ListViewItem();
            var j = jobs[index];
            var item = new ListViewItem(j.DisplayName);
            item.Tag = j;
            return item;
        }

        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (runingItems != null && e.ItemIndex >= firstItem && e.ItemIndex < firstItem + runingItems.Length)
            {
                //A cache hit, so get the ListViewItem from the cache instead of making a new one.
                if (runingItems.All(x => !((x.Tag as Job)?.IsDone ?? true)))
                {
                    e.Item = runingItems[e.ItemIndex - firstItem];
                    return;
                }
                runingItems = null;
            }
            //A cache miss, so create a new ListViewItem and pass it back.
            e.Item = GetListViewItem(e.ItemIndex);
        }

        private void listView1_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            //We've gotten a request to refresh the cache.
            //First check if it's really neccesary.
            if (runingItems != null && e.StartIndex >= firstItem && e.EndIndex <= firstItem + runingItems.Length)
            {
                //If the newly requested cache is a subset of the old cache, 
                //no need to rebuild everything, so do nothing.
                if (runingItems.All(x => !(x.Tag as Job).IsDone))
                    return;
            }

            //Now we need to rebuild the cache.
            firstItem = e.StartIndex;
            int length = e.EndIndex - e.StartIndex + 1; //indexes are inclusive
            runingItems = new ListViewItem[length];

            //Fill the cache with the appropriate ListViewItems.
            for (int i = 0; i < length; i++)
            {
                runingItems[i] = GetListViewItem(i + firstItem);
            }
        }

        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var job = e.Item.Tag as Job;

            if (job == null) return;

            if (job.Progress < 0)
            {
                // Draw the background for an unselected item.
                using (var brush = new LinearGradientBrush(e.Bounds, Color.LightBlue, Color.LightBlue, LinearGradientMode.Horizontal))
                {
                    brush.InterpolationColors = new ColorBlend()
                    {
                        Colors = new Color[] { Color.LightBlue, Color.White, Color.LightBlue },
                        Positions = new float[] { 0, 1 / 2f, 1 }
                    };
                    brush.TranslateTransform(tic * 10, 0);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
            }
            else if (job.Progress < 1)
            {
                var rect = new Rectangle(e.Bounds.Location, new Size((int)(e.Bounds.Width * job.Progress), e.Bounds.Height));
                if(job.IsInfo)
                    e.Graphics.FillRectangle(Brushes.Plum, rect);
                else
                    e.Graphics.FillRectangle(Brushes.LightBlue, rect);
            }
            else if (job.Progress >= 10)
            {
                var rect = new Rectangle(e.Bounds.Location, new Size((int)(e.Bounds.Width * (job.Progress - 10)), e.Bounds.Height));
                e.Graphics.FillRectangle(Brushes.LightYellow, rect);
            }
            else if (job.Progress > 1)
            {
                e.Graphics.FillRectangle(Brushes.LightYellow, e.Bounds);
            }
            else if (double.IsNaN(job.Progress))
            {
                e.Graphics.FillRectangle(Brushes.OrangeRed, e.Bounds);
            }
            else
            {
                e.Graphics.FillRectangle(Brushes.LightGreen, e.Bounds);
            }

            if ((e.State & ListViewItemStates.Selected) != 0)
            {
                e.DrawFocusRectangle();
            }

            var rect1 = e.Bounds;
            rect1.Height /= 2;
            var rect2 = new Rectangle(rect1.X, rect1.Y + rect1.Height, rect1.Width, rect1.Height);

            e.Graphics.DrawString(
                        job.DisplayName,
                        e.Item.Font,
                        SystemBrushes.WindowText,
                        rect1);
            e.Graphics.DrawString(
                        job.ProgressStr,
                        e.Item.Font,
                        SystemBrushes.WindowText,
                        rect2);
        }

        private void FormTaskList_Load(object sender, EventArgs e)
        {
            listView1.Columns[0].Width = listView1.ClientSize.Width;
            var imlist = new ImageList();
            imlist.ImageSize = new Size(1, 35);
            listView1.SmallImageList = imlist;
            FixPosition();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            JobControler.CancelAllforUser();
            TSviewCloud.FormClosing.Instance.Show();
        }

        private void listView1_ClientSizeChanged(object sender, EventArgs e)
        {
            listView1.BeginUpdate();
            listView1.Columns[0].Width = listView1.ClientSize.Width;
            listView1.EndUpdate();
        }

        bool manualchange = false;
        bool manuallocation = false;
        public void FixPosition()
        {
            if (manuallocation) return;

            if (TSviewCloud.Program.MainForm != null)
            {
                var s = Screen.FromControl(TSviewCloud.Program.MainForm);

                Point p = new Point(TSviewCloud.Program.MainForm.Left + TSviewCloud.Program.MainForm.Width, TSviewCloud.Program.MainForm.Top);
                Rectangle r = new Rectangle(p, Size);
                if (s.WorkingArea.Contains(r))
                {
                    Location = p;
                }
                else
                {
                    Point p2 = new Point(TSviewCloud.Program.MainForm.Left - Width, TSviewCloud.Program.MainForm.Top);
                    Rectangle r2 = new Rectangle(p2, Size);
                    if (s.WorkingArea.Contains(r2))
                    {
                        Location = p2;
                    }
                    else
                    {
                        Location = p;
                    }
                }
            }
        }

        private void FormTaskList_LocationChanged(object sender, EventArgs e)
        {
            if (manualchange) manuallocation = true;
        }

        private void FormTaskList_ResizeBegin(object sender, EventArgs e)
        {
            manualchange = true;
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (!Instance.Visible && (JobControler.JobList()?.Any(x => x.IsDelayShow) ?? false))
            {
                Instance.Show();
                Instance.BringToFront();
            }
        }
    }

    public class ConsoleJobDisp
    {
        static System.Threading.Timer t;
        static public void Run()
        {
            if (TSviewCloud.ConsoleFunc.IsOutputRedirected) return;
            t = new System.Threading.Timer((o) =>
            {
                Console.Clear();
                var height = Console.WindowHeight;
                var width = Console.WindowWidth;
                var joblist = JobControler.JobList();
                var i = 0;
                foreach (var j in joblist.Where(x => !x.IsHidden).OrderBy(x => x.Index))
                {
                    Console.SetCursorPosition(0, i++);
                    Console.Write(j.DisplayName);
                    if (i > height - 1) break;
                    Console.SetCursorPosition(4, i++);
                    Console.Write(j.ProgressStr);
                    if (i > height - 1) break;
                }
            }, null, 0, 1000);
        }
    }

    public class JobControler
    {
        public class SubInfo
        {
            public long index
            {
                get; internal set;
            }
            public long size;
            public long pos
            {
                set
                {
                    if (value > 0)
                    {
                        if (type == SubType.UploadFile)
                        {
                            UploadProgress.AddOrUpdate(index, value, (key, val) => value);
                        }
                        else if (type == SubType.DownloadFile)
                        {
                            DownloadProgress.AddOrUpdate(index, value, (key, val) => value);
                        }
                    }
                }
            }
            public enum SubType
            {
                unknown,
                UploadFile,
                UploadDirectory,
                DownloadFile,
            }
            public SubType type;
        }

        static ConcurrentBag<Job> joblist = new ConcurrentBag<Job>();
        static ConcurrentDictionary<JobClass, ConcurrentBag<WeakReference<Job>>> joblist_type = new ConcurrentDictionary<JobClass, ConcurrentBag<WeakReference<Job>>>();
        static BlockingCollection<WeakReference<Job>> StartQueue = new BlockingCollection<WeakReference<Job>>();
        static BlockingCollection<Task> EndQueue = new BlockingCollection<Task>();
        static bool jobempty = true;
        static internal ManualResetEventSlim joberaser = new ManualResetEventSlim(false);

        static SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        public static bool IsReloading = false;
        public static long UploadAll = 0;
        public static long UploadCur = 0;
        public static long UploadFileAll = 0;
        public static long UploadFileDone = 0;
        public static long UploadFolderAll = 0;
        public static long UploadFolderDone = 0;
        public static long UploadTotal = 0;
        public static long DownloadAll = 0;
        public static long DownloadCur = 0;
        public static long DownloadDone = 0;
        public static long DownloadTotal = 0;
        public static ConcurrentDictionary<long, long> UploadProgress = new ConcurrentDictionary<long, long>();
        public static long UploadProgressDone = 0;
        public static ConcurrentDictionary<long, long> DownloadProgress = new ConcurrentDictionary<long, long>();
        public static long DownloadProgressDone = 0;

        static public bool IsEmpty
        {
            get { return joblist.IsEmpty && jobempty; }
        }

        static void TriggerDisplay()
        {
            synchronizationContext.Post((o) =>
            {
                if (!IsEmpty && !FormTaskList.Instance.Visible)
                {
                    FormTaskList.Instance.Display();
                }
            }, null);
        }

        public static void ErrorOut(string str)
        {
            synchronizationContext.Post((o) =>
            {
                if (!IsEmpty && !TSviewCloud.FormErrorLog.Instance.Visible && joblist.Any(x => !x.IsHidden))
                {
                    TSviewCloud.FormErrorLog.Instance.Show();
                    TSviewCloud.FormErrorLog.Instance.BringToFront();
                }
                TSviewCloud.FormErrorLog.Instance.ErrorLog(str);
            }, null);
        }

        public static void ErrorOut(string format, params object[] args)
        {
            ErrorOut(string.Format(format, args));
        }

        static Job UploadInfoJob;
        static Job DownloadInfoJob;

        static private Job InternalCreateJob<T>(JobClass type, SubInfo info) where T : class
        {
            var newjob = new Job<T>()
            {
                JobType = type
            };
            joblist.Add(newjob);
            jobempty = false;
            if (type == JobClass.Upload)
            {
                if(UploadInfoJob == null)
                {
                    UploadInfoJob = CreateNewJob(JobClass.UploadInfo);
                    UploadInfoJob.DisplayName = "Upload Progress";
                    UploadInfoJob.StartTime = DateTime.Now;
                }
                Interlocked.Increment(ref UploadAll);
                newjob.Index = Interlocked.Increment(ref UploadCur);
                info.index = newjob.Index;
                newjob.JobInfo = info;
                if (info?.type == SubInfo.SubType.UploadFile)
                {
                    Interlocked.Increment(ref UploadFileAll);
                    Interlocked.Add(ref UploadTotal, info.size);
                }
                else if (info?.type == SubInfo.SubType.UploadDirectory)
                {
                    Interlocked.Increment(ref UploadFolderAll);
                }
            }
            else if (type == JobClass.Download)
            {
                if (DownloadInfoJob == null)
                {
                    DownloadInfoJob = CreateNewJob(JobClass.DownloadInfo);
                    DownloadInfoJob.DisplayName = "Download Progress";
                    DownloadInfoJob.StartTime = DateTime.Now;
                }
                Interlocked.Increment(ref DownloadAll);
                newjob.Index = Interlocked.Increment(ref DownloadCur);
                info.index = newjob.Index;
                newjob.JobInfo = info;
                if (info?.type == SubInfo.SubType.DownloadFile)
                {
                    Interlocked.Add(ref DownloadTotal, info.size);
                }
            }
            joblist_type.AddOrUpdate(type,
                (key) =>
                {
                    var newitem = new ConcurrentBag<WeakReference<Job>>
                    {
                        new WeakReference<Job>(newjob)
                    };
                    return newitem;
                },
                (key, value) =>
                {
                    value.Add(new WeakReference<Job>(newjob));
                    return value;
                });
            return newjob;
        }

        static public Job CreateNewJob(JobClass type = JobClass.Normal, SubInfo info = null)
        {
            return CreateNewJob<object>(type, info) as Job;
        }

        static public Job<T> CreateNewJob<T>(JobClass type = JobClass.Normal, SubInfo info = null) where T: class
        {
            var newjob = InternalCreateJob<T>(type, info) as Job<T>;
            TriggerDisplay();
            return newjob;
        }

        static public Job CreateNewJob(JobClass type = JobClass.Normal, SubInfo info = null, params Job[] depends)
        {
            return CreateNewJob<object>(type, info, depends) as Job;
        }

        static public Job<T> CreateNewJob<T>(JobClass type = JobClass.Normal, SubInfo info = null, params Job[] depends) where T : class
        {
            var newjob = InternalCreateJob<T>(type, info) as Job<T>;
            foreach (var d in depends)
            {
                if(d != null)
                    newjob.DependsOn.Enqueue(new WeakReference<Job>(d));
            }
            TriggerDisplay();
            return newjob;
        }

        static public void Run(Job target, Action<Job> JobAction)
        {
            if (target == null) return;
            target.JobAction = JobAction;
            StartQueue.Add(new WeakReference<Job>(target));
        }

        static public void Run<T>(Job target, Action<Job<T>> JobAction) where T: class
        {
            if (target == null) return;
            if(JobAction != null)
                target.JobAction = (j)=> { JobAction?.Invoke(j as Job<T>); };
            StartQueue.Add(new WeakReference<Job>(target));
        }

        static private bool CancelCheck(Job j)
        {
            if ((!(j.DependsOn?.IsEmpty ?? true) && j.DependsOn.Any(x => (x.TryGetTarget(out var y)? y.IsCanceled || y.IsError : false))))
            {
                if (!j.DoAlways)
                {
                    j.Cancel();
                    return true;
                }
            }
            return false;
        }


        static private int delcount = 0;

        static private void DeleteJob()
        {
            if (Interlocked.Increment(ref delcount) > 1)
            {
                Interlocked.Decrement(ref delcount);
                return;
            }
            foreach (var key in joblist_type.Keys)
            {
                if (joblist_type[key].All(x => (x.TryGetTarget(out var tmp1)) ? tmp1._isdeleted : true))
                {
                    ConcurrentBag<WeakReference<Job>> prevval;
                    if (joblist_type.TryRemove(key, out prevval))
                    {
                        if (prevval.Any(x => (x.TryGetTarget(out var tmp1)) ? !tmp1._isdeleted : false))
                        {
                            foreach (var reJob in prevval.Where(x => x.TryGetTarget(out var tmp1) ? !tmp1._isdeleted : false))
                            {
                                joblist_type.AddOrUpdate(key,
                                    (key1) =>
                                    {
                                        var newitem = new ConcurrentBag<WeakReference<Job>>();
                                        newitem.Add(reJob);
                                        return newitem;
                                    },
                                    (key1, value) =>
                                    {
                                        value.Add(reJob);
                                        return value;
                                    });
                            }
                        }
                    }
                }
            }
            var oldjoblist = joblist.ToArray();
            var keepjob = new List<Job>();
            foreach (var j in oldjoblist)
            {
                if (!j._isdeleted)
                {
                    keepjob.Add(j);
                    foreach (var dj in j.DependsOn)
                    {
                        if(dj.TryGetTarget(out var t))
                            keepjob.Add(t);
                    }
                }
                else if (keepjob.Contains(j))
                {
                    foreach (var dj in j.DependsOn)
                    {
                        if (dj.TryGetTarget(out var t))
                            keepjob.Add(t);
                    }
                }
            }
            keepjob = keepjob.Distinct().ToList();
            var dellist = oldjoblist.Except(keepjob).ToArray();
            if (dellist.Count() > 0)
            {
                var newjoblist = new ConcurrentBag<Job>();
                foreach (var j in keepjob)
                {
                    newjoblist.Add(j);
                }
                lock (joblist)
                {
                    joblist = newjoblist;
                }
                foreach (var rm in dellist)
                {
                    rm.Dispose();
                }
            }

            if (joblist.IsEmpty)
            {
                Interlocked.Exchange(ref UploadAll, 0);
                Interlocked.Exchange(ref UploadCur, 0);
                Interlocked.Exchange(ref UploadFileAll, 0);
                Interlocked.Exchange(ref UploadFileDone, 0);
                Interlocked.Exchange(ref UploadFolderAll, 0);
                Interlocked.Exchange(ref UploadFolderDone, 0);
                Interlocked.Exchange(ref UploadTotal, 0);
                Interlocked.Exchange(ref DownloadAll, 0);
                Interlocked.Exchange(ref DownloadCur, 0);
                Interlocked.Exchange(ref DownloadDone, 0);
                Interlocked.Exchange(ref DownloadTotal, 0);
                UploadProgress.Clear();
                Interlocked.Exchange(ref UploadProgressDone, 0);
                DownloadProgress.Clear();
                Interlocked.Exchange(ref DownloadProgressDone, 0);
                jobempty = true;
            }
            else
            {
                Task.Delay(100).ContinueWith((t) => DeleteJob());
            }
            Interlocked.Decrement(ref delcount);
        }

        static System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        static int jobcount = 0;

        static readonly JobClass[] singlejob =
            {
                JobClass.Trash,
                JobClass.Play,
                JobClass.PlayDownload,
            };

        static void JobStartCheck()
        {
            Parallel.ForEach(joblist_type.Keys,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                (jobtype) =>
                {
                    var running_count = 0;
                    var max_running = int.MaxValue;
                    try
                    {
                        if (jobtype == JobClass.Download)
                        {
                            running_count = joblist_type[jobtype].Where(x => (x.TryGetTarget(out var tmp)) ? tmp.IsRunning : false).Count();
                            max_running = TSviewCloudConfig.Config.ParallelDownload;
                        }
                        else if (jobtype == JobClass.Upload)
                        {
                            running_count = joblist_type[jobtype].Where(x => (x.TryGetTarget(out var tmp)) ? tmp.IsRunning : false).Count();
                            max_running = TSviewCloudConfig.Config.ParallelUpload;
                        }
                        else if (jobtype == JobClass.LoadItem)
                        {
                            running_count = joblist_type[jobtype].Where(x => (x.TryGetTarget(out var tmp)) ? tmp.IsRunning : false).Count();
                            max_running = 20;
                        }
                        else if (singlejob.Contains(jobtype))
                        {
                            running_count = joblist_type[jobtype].Where(x => (x.TryGetTarget(out var tmp)) ? tmp.IsRunning : false).Count();
                            max_running = 1;
                        }

                        foreach (var wj in joblist_type[jobtype]
                            .Where(x => (x.TryGetTarget(out var tmp)) ? !tmp._delete : false)
                            .OrderBy(x => (x.TryGetTarget(out var tmp)) ? tmp.Priority : 0)
                            .ThenBy(x => (x.TryGetTarget(out var tmp)) ? tmp.QueueTime : default(DateTime)).ToArray())
                        {
                            if (!wj.TryGetTarget(out var j)) continue;
                            if (j.JobTask != null)
                            {
                                var canceled = CancelCheck(j);
                                if (!j.IsDone && !j.IsRunning)
                                {
                                    if (!canceled && (j.DependsOn.IsEmpty || j.WeekDepend || j.DependsOn.All(x => (x.TryGetTarget(out var y)) ? y._delete || y.IsDone : true)))
                                    {
                                        j.resultOfDepend = j.DependsOn.Select(x => (x.TryGetTarget(out var y)) ? new WeakReference<object>(y.result) : null).ToArray();

                                        if (running_count < max_running)
                                        {
                                            j.StartTime = DateTime.Now;
                                            running_count++;
                                            Interlocked.Increment(ref jobcount);
                                            j.JobTask.Start();
                                        }
                                        if (running_count >= max_running)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                });
            DeleteJob();
        }
           

        static JobControler()
        {
            Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var weakstartjob = StartQueue.Take();
                        if (!weakstartjob.TryGetTarget(out var startjob))
                        {
                            RemoveJob(weakstartjob);
                            continue;
                        }
                        else if(startjob == null)
                        {
                            continue;
                        }
                        else if(startjob.JobAction == null)
                        {
                            startjob._done = true;
                            RemoveJob(startjob);
                            continue;
                        }
                        if (startjob.Ct.IsCancellationRequested)
                        {
                            startjob._done = true;
                            Task.Delay(5000).ContinueWith((t) => RemoveJob(startjob));
                            continue;
                        }
                        if (startjob.JobType == JobClass.Play)
                        {
                            foreach (var i in joblist.Where(x => x != startjob).Where(x => x.JobType == JobClass.Play))
                                i.Cancel();
                            foreach (var i in joblist.Where(x => x.JobType == JobClass.PlayDownload).Where(x => !x.DependsOn.Select(y => y.TryGetTarget(out var z) ? z : null).Contains(startjob)))
                                i.Cancel();
                        }
                        startjob.QueueTime = DateTime.Now;
                        startjob.JobTask = new Task((o)=> {
                            Job j = null;
                            if((o as WeakReference<Job>).TryGetTarget(out j) && j != null)
                                startjob.JobAction(j);
                        }, new WeakReference<Job>(startjob), startjob.Ct);
                        EndQueue.Add(startjob.JobTask.ContinueWith((t, weaktarget) =>
                        {
                            Interlocked.Decrement(ref jobcount);
                            if (!(weaktarget as WeakReference<Job>).TryGetTarget(out var s))
                                return;
                            s._done = true;
                            s.FinishTime = DateTime.Now;
                            s.JobAction = null;
                            s.resultOfDepend = null;
                            if(s.JobInfo != null)
                            {
                                long val;
                                switch (s.JobInfo.type)
                                {
                                    case SubInfo.SubType.UploadFile:
                                        while (!UploadProgress.TryRemove(s.Index, out val))
                                            if(!UploadProgress.TryGetValue(s.Index, out val))
                                                break;
                                        Interlocked.Add(ref UploadProgressDone, s.JobInfo.size);
                                        Interlocked.Increment(ref UploadFileDone);
                                        break;
                                    case SubInfo.SubType.UploadDirectory:
                                        Interlocked.Increment(ref UploadFolderDone);
                                        break;
                                    case SubInfo.SubType.DownloadFile:
                                        while (!DownloadProgress.TryRemove(s.Index, out val))
                                            if (!DownloadProgress.TryGetValue(s.Index, out val))
                                                break;
                                        Interlocked.Add(ref DownloadProgressDone, s.JobInfo.size);
                                        Interlocked.Increment(ref DownloadDone);
                                        break;
                                }
                            }
                            if(UploadInfoJob != null)
                            {
                                if(UploadFileDone + UploadFolderDone >= UploadAll)
                                {
                                    UploadInfoJob._done = true;
                                    UploadInfoJob._delete = true;
                                    var u = UploadInfoJob;
                                    Task.Delay(5000).ContinueWith((t2) =>
                                    {
                                        RemoveJob(u);
                                    });
                                    UploadInfoJob = null;
                                }
                            }
                            if (DownloadInfoJob != null)
                            {
                                if (DownloadDone >= DownloadAll)
                                {
                                    DownloadInfoJob._done = true;
                                    DownloadInfoJob._delete = true;
                                    var d = DownloadInfoJob;
                                    Task.Delay(5000).ContinueWith((t2) =>
                                    {
                                        RemoveJob(d);
                                    });
                                    DownloadInfoJob = null;
                                }
                            }
                            if (t.IsCanceled)
                            {
                                s.ProgressStr = "Operation canceled.";
                                s.Progress += 10;
                            }
                            if (t.IsFaulted)
                            {
                                var e = t.Exception;
                                e.Flatten().Handle(ex =>
                                {
                                    if (ex is OperationCanceledException)
                                    {
                                        s.ProgressStr = "Operation canceled.";
                                        s.Progress += 10;
                                    }
                                    else
                                    {
                                        TSviewCloudConfig.Config.Log.LogOut("TaskError : ERROR {0}", ex.ToString());
                                        ErrorOut("TaskError : ERROR {0}", ex.ToString());
                                        s.ProgressStr = ex.Message;
                                        s.Progress = double.NaN;
                                    }
                                    return true;
                                });
                                e.Handle(ex =>
                                {
                                    return true;
                                });
                            }
                            s._delete = true;
                            if (s.JobType == JobClass.Save)
                            {
                                RemoveJob(s);
                            }
                            else if (s.JobType == JobClass.LoadItem)
                            {
                                Task.Delay(1000).ContinueWith((task) => RemoveJob(s));
                            }
                            else
                            {
                                Task.Delay(5000).ContinueWith((task) => RemoveJob(s));
                            }
                            joberaser.Set();
                        }, new WeakReference<Job>(startjob)));
                        joberaser.Set();
                        startjob._start.Set();
                    }
                }
                catch { }
            });
            Task.Run(() => {
                try
                {
                    while (true)
                    {
                        joberaser.Wait();
                        joberaser.Reset();
                        JobStartCheck();
                    }
                }
                catch { throw; }
            });
            Task.Run(() => {
                while (true)
                {
                    var t = EndQueue.Take();
                    try
                    {
                        t.Wait();
                    }
                    catch { }
                }
            });
        }

        static public void RemoveJob(Job doneJob)
        {
            doneJob._isdeleted = true;
            joberaser.Set();
        }
        static public void RemoveJob(WeakReference<Job> doneJob)
        {
            if(doneJob.TryGetTarget(out var tmp))
                tmp._isdeleted = true;
            joberaser.Set();
        }

        static public bool CancelAll()
        {
            lock (joblist)
            {
                foreach (var j in joblist.Where(x => !x._delete).Where(x => x.JobType != JobClass.Save))
                {
                    j.Cancel();
                }
            }
            return !IsEmpty;
        }

        static public bool CancelAllforUser()
        {
            lock (joblist)
            {
                foreach (var j in joblist.Where(x => !x._delete).Where(x => x.JobType != JobClass.Save))
                {
                    j.Cancel();
                }
            }
            return !IsEmpty;
        }

        static public void CancelPlay()
        {
            lock (joblist)
            {
                foreach (var j in joblist.Where(x => !x._delete).Where(x => x.JobType == JobClass.Play || x.JobType == JobClass.PlayDownload))
                    j.Cancel();
            }
        }

        static public Job[] JobList()
        {
            var list = joblist.Where(x => !x._isdeleted).ToArray();
            return list;
        }

        static public int JobTypeCount(JobClass type)
        {
            ConcurrentBag<WeakReference<Job>> ret;
            if (joblist_type.TryGetValue(type, out ret))
            {
                return ret.Where(x => (x.TryGetTarget(out var tmp))? !tmp._isdeleted: false).Count();
            }
            else
            {
                return 0;
            }
        }

        static public string ConvertUnit(double rate)
        {
            if (rate < 1024)
                return string.Format("{0:#,0.00}Byte/s", rate);
            if (rate < 1024 * 1024)
                return string.Format("{0:#,0.00}KiB/s", rate / 1024);
            if (rate < 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}MiB/s", rate / 1024 / 1024);
            if (rate < (double)1024 * 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}GiB/s", rate / 1024 / 1024 / 1024);
            if (rate < (double)1024 * 1024 * 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}TiB/s", rate / 1024 / 1024 / 1024 / 1024);
            return string.Format("{0:#,0.00}PiB/s", rate / 1024 / 1024 / 1024 / 1024);
        }
    }

 }
