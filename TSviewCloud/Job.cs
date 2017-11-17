using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TSviewCloudPlugin
{
    public enum JobClass
    {
        Normal,
        Display,
        LoadItem,
        Save,
        RemoteDownload,
        RemoteOperation,
        Download,
        Upload,
        Trash,
        Play,
        PlayDownload,
        Clean,
        ControlMaster,
        UploadInfo,
        DownloadInfo,
    }


    public abstract class JobBase: IDisposable
    {
        private string _ProgressStr;
        private string displayName;
        public virtual string ProgressStr
        {
            get
            {
                switch (JobType)
                {
                    case JobClass.Upload:
                        return string.Format("Upload({0}/{1}) : {2}", Index, JobControler.UploadAll, _ProgressStr);
                    case JobClass.Download:
                        return string.Format("Download({0}/{1}) : {2}", Index, JobControler.DownloadAll, _ProgressStr);
                    case JobClass.UploadInfo:
                        {
                            var sb = new StringBuilder();
                            var subtotal = JobControler.UploadProgress.Aggregate(0L, (acc, kvp) => acc + kvp.Value);
                            sb.Append("Upload ");
                            sb.AppendFormat("File({0}/{1}) ", JobControler.UploadFileDone, JobControler.UploadFileAll);
                            sb.AppendFormat("Folder({0}/{1}) ", JobControler.UploadFolderDone, JobControler.UploadFolderAll);
                            Progress = (double)(subtotal + JobControler.UploadProgressDone) / JobControler.UploadTotal;
                            if (double.IsNaN(Progress)) Progress = 1;
                            sb.AppendFormat("{0:#,0}/{1:#,0}({2:0.00%}) ", subtotal + JobControler.UploadProgressDone, JobControler.UploadTotal, Progress);
                            var speed = (subtotal + JobControler.UploadProgressDone) / (DateTime.Now - StartTime).TotalSeconds;
                            var togo = Math.Round((JobControler.UploadTotal - subtotal - JobControler.UploadProgressDone) / speed);
                            togo = (double.IsInfinity(togo) || double.IsNaN(togo)) ? 0 : togo;
                            sb.AppendFormat("{0} [to go {1}]", JobControler.ConvertUnit(speed), TimeSpan.FromSeconds(togo));
                            return sb.ToString();
                        }
                    case JobClass.DownloadInfo:
                        {
                            var sb = new StringBuilder();
                            var subtotal = JobControler.DownloadProgress.Aggregate(0L, (acc, kvp) => acc + kvp.Value);
                            sb.Append("Download ");
                            sb.AppendFormat("({0}/{1}) ", JobControler.DownloadDone, JobControler.DownloadAll);
                            Progress = (double)(subtotal + JobControler.DownloadProgressDone) / JobControler.DownloadTotal;
                            if (double.IsNaN(Progress)) Progress = 1;
                            sb.AppendFormat("{0:#,0}/{1:#,0}({2:0.00%}) ", subtotal + JobControler.DownloadProgressDone, JobControler.DownloadTotal, Progress);
                            var speed = (subtotal + JobControler.DownloadProgressDone) / (DateTime.Now - StartTime).TotalSeconds;
                            var togo = Math.Round((JobControler.DownloadTotal - subtotal - JobControler.DownloadProgressDone) / speed);
                            togo = (double.IsInfinity(togo) || double.IsNaN(togo)) ? 0 : togo;
                            sb.AppendFormat("{0} [to go {1}]", JobControler.ConvertUnit(speed), TimeSpan.FromSeconds(togo));
                            return sb.ToString();
                        }
                    default:
                        return _ProgressStr;
                }
            }
            set { _ProgressStr = value; }
        }
        public virtual bool IsInfo
        {
            get { return (JobType == JobClass.UploadInfo || JobType == JobClass.DownloadInfo) ? true : false; }
        }
        private double progress = 0;
        internal object result;
        internal WeakReference<object>[] resultOfDepend;
        private long index;
        public virtual JobClass JobType
        {
            get; internal set;
        }
        public virtual JobControler.SubInfo JobInfo
        {
            get; internal set;
        }
        private CancellationTokenSource cts = new CancellationTokenSource();
        internal ConcurrentQueue<WeakReference<Job>> DependsOn = new ConcurrentQueue<WeakReference<Job>>();
        internal Action<Job> JobAction;
        internal Task JobTask;
        internal bool _delete = false;
        internal bool _isdeleted = false;
        public virtual DateTime QueueTime
        {
            get; internal set;
        }
        public virtual DateTime StartTime
        {
            get; internal set;
        }
        public virtual DateTime FinishTime
        {
            get; internal set;
        }
        private bool isError = false;
        private bool doAlways = false;
        private bool weekDepend = false;
        internal bool _done = false;
        internal ManualResetEvent _start = new ManualResetEvent(false);
        internal ManualResetEvent _run = new ManualResetEvent(false);

        private bool forceHidden = false;

        public virtual CancellationToken Ct
        {
            get { return cts.Token; }
        }
        public virtual CancellationTokenSource Cts
        {
            get { return cts; }
        }

        public virtual void Cancel()
        {
            cts.Cancel();
            if (!IsRunning)
            {
                _delete = true;
                Task.Delay(5000).ContinueWith((task) => JobControler.RemoveJob(this as Job));
                JobControler.joberaser.Set();
            }
        }

        public virtual void Wait(int timeout = -1, CancellationToken ct = default(CancellationToken))
        {
            if (cts.IsCancellationRequested) return;
            WaitHandle.WaitAny(new WaitHandle[] { _start, ct.WaitHandle });
            JobTask?.Wait(timeout, ct);
        }

        public virtual Task WaitTask(int timeout = -1, CancellationToken ct = default(CancellationToken))
        {
            return Task.Run(() =>
            {
                WaitHandle.WaitAny(new WaitHandle[] { _start, ct.WaitHandle });
                JobTask?.Wait(timeout, ct);
            }, ct);
        }

        public virtual bool IsDone
        {
            get
            {
                return cts.Token.IsCancellationRequested
                    || (JobTask?.IsCanceled ?? false)
                    || (JobTask?.IsCompleted ?? false)
                    || (JobTask?.IsFaulted ?? false)
                    || _done;
            }
        }
        public virtual bool IsCanceled
        {
            get
            {
                return cts.Token.IsCancellationRequested
                    || ((DoAlways) ? false : DependsOn?.Any(x => (x.TryGetTarget(out var y))? y.IsCanceled: false) ?? false)
                    || (JobTask?.IsCanceled ?? false);
            }
        }
        public virtual bool IsRunning
        {
            get
            {
                return JobTask?.Status == TaskStatus.Running
                  || JobTask?.Status == TaskStatus.WaitingToRun;
            }
        }
        public virtual bool IsHidden
        {
            get
            {
                return JobType == JobClass.Clean
                    || JobType == JobClass.ControlMaster
                    || JobType == JobClass.Display
                    || ForceHidden;
            }
        }
        public virtual bool IsDelayShow
        {
            get
            {
                return (!IsHidden && JobType != JobClass.LoadItem)
                    || (JobType == JobClass.LoadItem && !IsDone && QueueTime.AddSeconds(2) < DateTime.Now);
            }
        }
        public virtual int Priority
        {
            get
            {
                if (JobType != JobClass.Upload) return 0;
                if (JobInfo?.type == JobControler.SubInfo.SubType.UploadFile) return 1;
                return 0;
            }
        }

        public virtual string DisplayName { get => displayName; set => displayName = value; }
        public virtual double Progress { get => progress; set => progress = value; }
        public virtual long Index { get => index; set => index = value; }
        public virtual bool IsError { get => isError; set => isError = value; }
        public virtual bool DoAlways { get => doAlways; set => doAlways = value; }
        public virtual bool WeekDepend { get => weekDepend; set => weekDepend = value; }
        public virtual bool ForceHidden { get => forceHidden; set => forceHidden = value; }

        public virtual void Error(string str)
        {
            Progress = double.NaN;
            IsError = true;
            ProgressStr = str;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。
                DependsOn = null;
                JobAction = null;
                result = null;
                resultOfDepend = null;
                DependsOn = null;

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~JobBase() {
        //   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //   Dispose(false);
        // }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }


    public class JobForToken : JobBase
    {
        protected CancellationToken prevCT;

        public JobForToken() : base() { }

        public JobForToken(CancellationToken ct) : base()
        {
            prevCT = ct;
            QueueTime = StartTime = FinishTime = DateTime.Now;
        }

        public override bool IsCanceled => (prevCT == default(CancellationToken)) ? base.IsCanceled : base.IsCanceled || prevCT.IsCancellationRequested;
        public override bool IsDone => (prevCT == default(CancellationToken)) ? base.IsDone : true;
        public override bool IsRunning => (prevCT == default(CancellationToken)) ? base.IsRunning : false;

        public override void Wait(int timeout = -1, CancellationToken ct = default(CancellationToken))
        {
            if (prevCT == default(CancellationToken))
                base.Wait(timeout, ct);
            else
                WaitHandle.WaitAny(new WaitHandle[] { prevCT.WaitHandle, ct.WaitHandle });
        }

        public override Task WaitTask(int timeout = -1, CancellationToken ct = default(CancellationToken))
        {
            if (prevCT == default(CancellationToken))
                return base.WaitTask(timeout, ct);
            else
                return Task.Run(() =>
                {
                    WaitHandle.WaitAny(new WaitHandle[] { prevCT.WaitHandle, ct.WaitHandle });
                }, ct);
        }
    }

    public class Job : JobForToken
    {
        public Job() : base()
        {
        }
        public Job(CancellationToken ct) : base(ct)
        {
        }
    }

    public class Job<T> : Job where T : class
    {
        public Job() : base()
        {
        }
        public Job(CancellationToken ct) : base(ct)
        {
        }

        public T Result { get => result as T; set => result = value; }
        public WeakReference<T>[] ResultOfDepend
        {
            get
            {
                return resultOfDepend.Select(x => {
                    if (x.TryGetTarget(out var target))
                    {
                        return new WeakReference<T>(target as T);
                    }
                    else
                    {
                        return null;
                    }
                    }).ToArray();
            }
        }
    }
}
