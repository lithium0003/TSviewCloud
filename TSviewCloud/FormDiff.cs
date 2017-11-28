using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSviewCloudPlugin;

namespace TSviewCloud
{
    public sealed partial class FormDiff : Form
    {
        private static readonly FormDiff _instance = new FormDiff();

        public static FormDiff Instance
        {
            get
            {
                return _instance;
            }
        }

        private FormDiff()
        {
            InitializeComponent();
            listBox_A.DataSource = _SelectedRemoteFilesA;
            listBox_B.DataSource = _SelectedRemoteFilesB;
        }

        private EventHandler _addACallback;
        private EventHandler _addBCallback;
        private IEnumerable<IRemoteItem> _SelectedRemoteFilesA;
        private IEnumerable<IRemoteItem> _SelectedRemoteFilesB;
        private Job runningJob;

        private IEnumerable<IRemoteItem> GetItems(IRemoteItem rootitem)
        {
            List<IRemoteItem> ret = new List<IRemoteItem>();

            if (rootitem == null) return ret;
            ret.Add(rootitem);

            var target = rootitem.Children;
            if (target == null) return ret;

            Parallel.ForEach(
                target,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                () => new List<IRemoteItem>(),
                (x, state, local) =>
                {
                    var item = RemoteServerFactory.PathToItem(x.FullPath);
                    if (item == null) return local;
                    local.Add(item);
                    local.AddRange(GetItems(item));
                    return local;
                },
                (result) =>
                {
                    lock (ret)
                        ret.AddRange(result);
                }
            );
            return ret;
        }

        public IEnumerable<IRemoteItem> SelectedRemoteFilesA
        {
            get
            {
                return _SelectedRemoteFilesA;
            }
            set
            {
                if (value == null)
                {
                    _SelectedRemoteFilesA = null;
                    listBox_A.DataSource = null;
                }
                else
                {
                    Cursor.Current = Cursors.WaitCursor;
                    _SelectedRemoteFilesA = value.ToArray()
                        .AsParallel()
                        .Select(x => GetItems(RemoteServerFactory.PathToItem(x.FullPath)))
                        .SelectMany(x => x.Select(y => y))
                        .Distinct()
                        .Where(x => x.ItemType == RemoteItemType.File);
                    listBox_A.DataSource = _SelectedRemoteFilesA.ToList();
                }
            }
        }
        public IEnumerable<IRemoteItem> SelectedRemoteFilesB
        {
            get
            {
                return _SelectedRemoteFilesB;
            }
            set
            {
                if (value == null)
                {
                    _SelectedRemoteFilesB = null;
                    listBox_B.DataSource = null;
                }
                else
                {
                    Cursor.Current = Cursors.WaitCursor;
                    _SelectedRemoteFilesB = value.ToArray()
                        .AsParallel()
                        .Select(x => GetItems(RemoteServerFactory.PathToItem(x.FullPath)))
                        .SelectMany(x => x.Select(y => y))
                        .Distinct()
                        .Where(x => x.ItemType == RemoteItemType.File);
                    listBox_B.DataSource = _SelectedRemoteFilesB.ToList();
                }
            }
        }

        public EventHandler AddACallback { get => _addACallback; set => _addACallback = value; }
        public EventHandler AddBCallback { get => _addBCallback; set => _addBCallback = value; }

        private void deltetItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(var i in listBox_A.SelectedIndices.OfType<int>().Reverse())
            {
                listBox_A.Items.RemoveAt(i);
            }
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {

        }

        static public string GetBasePathRemote(IEnumerable<string> paths)
        {
            string prefix = null;
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (prefix == null)
                {
                    if (p.EndsWith("://"))
                        prefix = p;
                    else
                        prefix = p.Substring(0, p.LastIndexOf('/')+1);
                }
                if (prefix == "")
                    break;
                while (!p.StartsWith(prefix) && prefix != "")
                {
                    if (prefix.EndsWith("://")) prefix = "";
                    else
                    {
                        prefix = prefix.Substring(0, prefix.Length - 1);
                        prefix = p.Substring(0, prefix.LastIndexOf('/')+1);
                    }
                }
            }
            return prefix ?? "";
        }

        public class RemoteItemInfo
        {
            public IRemoteItem info;
            public string path;
            public string name;
            public RemoteItemInfo(IRemoteItem info, string path, string name)
            {
                this.info = info;
                this.path = path;
                this.name = name;
            }
        }

        public class MatchItem
        {
            public RemoteItemInfo remoteA;
            public RemoteItemInfo remoteB;
            public MatchItem(RemoteItemInfo remoteA, RemoteItemInfo remoteB)
            {
                this.remoteA = remoteA;
                this.remoteB = remoteB;
            }
        }

        private void button_start_Click(object sender, EventArgs e)
        {
            if (SelectedRemoteFilesA == null) return;
            if (SelectedRemoteFilesB == null) return;

            ConcurrentBag<MatchItem> RemoteAOnly = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> RemoteBOnly = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> BothAndMatch = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> BothAndUnmatch = new ConcurrentBag<MatchItem>();
            ConcurrentDictionary<string, RemoteItemInfo[]> RemoteDupA = new ConcurrentDictionary<string, RemoteItemInfo[]>();
            ConcurrentDictionary<string, RemoteItemInfo[]> RemoteDupB = new ConcurrentDictionary<string, RemoteItemInfo[]>();

            var synchronizationContext = SynchronizationContext.Current;
            bool TreeFlag = radioButton_Tree.Checked;
            bool FilenameFlag = radioButton_filename.Checked;
            bool HashFlag = radioButton_Hash.Checked;

            var job = JobControler.CreateNewJob();
            job.DisplayName = "Match";
            job.ProgressStr = "wait for run";
            runningJob = job;
            bool done = false;
            JobControler.Run(job, (j) =>
            {
                job.ProgressStr = "running...";
                job.Progress = -1;

                synchronizationContext.Post((o) =>
                {
                    button_start.Enabled = false;
                }, null);

                var remoteA = SelectedRemoteFilesA.Select(x => new RemoteItemInfo(x, x.FullPath, null)).ToArray();
                var remotebasepathA = GetBasePathRemote(remoteA.Select(x => x.path));

                if (TreeFlag)
                    remoteA = remoteA.Select(x => new RemoteItemInfo(x.info, x.path, x.path.Substring(remotebasepathA.Length))).ToArray();
                if (FilenameFlag)
                    remoteA = remoteA.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Name)).ToArray();
                if (HashFlag)
                    remoteA = remoteA.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Hash ?? "")).ToArray();

                var remoteB = SelectedRemoteFilesB.Select(x => new RemoteItemInfo(x, x.FullPath, null)).ToArray();
                var remotebasepathB = GetBasePathRemote(remoteB.Select(x => x.path));

                if (TreeFlag)
                    remoteB = remoteB.Select(x => new RemoteItemInfo(x.info, x.path, x.path.Substring(remotebasepathB.Length))).ToArray();
                if (FilenameFlag)
                    remoteB = remoteB.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Name)).ToArray();
                if (HashFlag)
                    remoteB = remoteB.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Hash ?? "")).ToArray();

                var len = remoteA.Count();
                int i = 0;
                foreach (var ritem in remoteA.GroupBy(x => x.name).Where(g => g.Count() > 1))
                {
                    RemoteDupA[ritem.Key] = ritem.ToArray();
                }
                foreach (var ritem in remoteB.GroupBy(x => x.name).Where(g => g.Count() > 1))
                {
                    RemoteDupB[ritem.Key] = ritem.ToArray();
                }
                var A = remoteA.GroupBy(x => x.name).ToArray();

                i = 0;
                Parallel.ForEach(A,
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (litem) =>
                    {
                        job.Ct.ThrowIfCancellationRequested();
                        var matchitem = remoteB.Where(x => x.name == litem.FirstOrDefault()?.name).ToArray();

                        if (litem.Count() > 1)
                        {
                            RemoteDupA[litem.Key] = litem.ToArray();
                        }

                        if (matchitem.Length > 0)
                        {
                            List<RemoteItemInfo> RemoteBMatched = new List<RemoteItemInfo>();
                            List<RemoteItemInfo> RemoteAUnMatched = new List<RemoteItemInfo>();
                            // match test
                            foreach (var item in litem)
                            {
                                Interlocked.Increment(ref i);
                                synchronizationContext.Send((o) =>
                                {
                                    if (runningJob?.Ct.IsCancellationRequested ?? true) return;
                                    label_info.Text = o as string;
                                }, string.Format("{0}/{1} {2}", i, len, item.path));

                                List<RemoteItemInfo> Matched = new List<RemoteItemInfo>();
                                foreach (var ritem in matchitem)
                                {
                                    if (item.info.Size == ritem.info.Size)
                                    {
                                        if (string.IsNullOrEmpty(item.info.Hash) || string.IsNullOrEmpty(ritem.info.Hash) || item.info.Hash == ritem.info.Hash)
                                        {
                                            Matched.Add(ritem);
                                        }
                                    }
                                }

                                if (Matched.Count() == 0)
                                {
                                    RemoteAUnMatched.Add(item);
                                }


                                Parallel.ForEach(Matched.Select(x => new MatchItem(item, x)), (x) => BothAndMatch.Add(x));
                                RemoteBMatched.AddRange(Matched);
                            }

                            var RemoteBUnMatched = matchitem.Except(RemoteBMatched);
                            if (RemoteBUnMatched.Count() < RemoteBUnMatched.Count())
                            {
                                Parallel.ForEach(RemoteBUnMatched.Concat(RemoteBMatched).Zip(RemoteAUnMatched, (r, l) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
                            }
                            else if (RemoteBUnMatched.Count() > RemoteAUnMatched.Count())
                            {
                                Parallel.ForEach(RemoteAUnMatched.Concat(litem).Zip(RemoteBUnMatched, (l, r) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
                            }
                            else
                            {
                                if (RemoteBUnMatched.Count() > 0)
                                    Parallel.ForEach(RemoteAUnMatched.Zip(RemoteBUnMatched, (l, r) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
                            }
                        }
                        else
                        {
                            //nomatch
                            foreach (var item in litem)
                            {
                                Interlocked.Increment(ref i);
                                synchronizationContext.Send((o) =>
                                {
                                    if (runningJob?.Ct.IsCancellationRequested ?? true) return;
                                    label_info.Text = o as string;
                                }, string.Format("{0}/{1} {2}", i, len, item.path));
                                RemoteAOnly.Add(new MatchItem(item, null));
                            }
                        }
                    });
                Parallel.ForEach(
                    remoteB.Select(x => x.info)
                    .Except(BothAndMatch.Select(x => x.remoteB.info))
                    .Except(BothAndUnmatch.Select(x => x.remoteB.info))
                    .Select(x => remoteB.Where(y => y.info == x).FirstOrDefault())
                    .Select(x => new MatchItem(null, x)),
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => RemoteBOnly.Add(x));
                done = true;
                job.Progress = 1;
                job.ProgressStr = "done.";
            });
            var afterjob = JobControler.CreateNewJob(JobClass.Clean, depends: job);
            afterjob.DisplayName = "clean up";
            afterjob.DoAlways = true;
            JobControler.Run(afterjob, (j) =>
            {
                afterjob.ProgressStr = "done.";
                afterjob.Progress = 1;
                runningJob = null;
                synchronizationContext.Post((o) =>
                {
                    label_info.Text = "";
                    button_start.Enabled = true;
                    if (done)
                    {
                        var result = new FormDiffResult();
                        result.RemoteOnly = RemoteBOnly.ToArray();
                        result.LocalOnly = RemoteAOnly.ToArray();
                        result.Unmatch = BothAndUnmatch.ToArray();
                        result.Match = BothAndMatch.ToArray();
                        result.RemoteBDup = RemoteDupB;
                        result.RemoteADup = RemoteDupA;
                        result.Show();
                    }
                }, null);
            });
        }

        private void FormMatch_FormClosing(object sender, FormClosingEventArgs e)
        {
            runningJob?.Cancel();
            Hide();
            e.Cancel = true;
        }

        private void listBox_remote_Format(object sender, ListControlConvertEventArgs e)
        {
            e.Value = (e.ListItem as IRemoteItem).FullPath;
        }

        private void button_AddRemoteA_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFilesB?.ToList();
            if (items == null)
            {
                AddACallback?.Invoke(this, new EventArgs());
            }
            else
            {
                AddACallback?.Invoke(this, new EventArgs());
                items.AddRange(SelectedRemoteFilesB);
                SelectedRemoteFilesB = items;
            }
        }

        private void button_AddRemoteB_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFilesB?.ToList();
            if (items == null)
            {
                AddBCallback?.Invoke(this, new EventArgs());
            }
            else
            {
                AddBCallback?.Invoke(this, new EventArgs());
                items.AddRange(SelectedRemoteFilesB);
                SelectedRemoteFilesB = items;
            }
        }

        private void toolStripMenuItemA_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFilesA?.ToList();
            if (items == null) return;
            foreach (var i in listBox_A.SelectedIndices.OfType<int>().Reverse())
            {
                items.RemoveAt(i);
            }
            SelectedRemoteFilesA = items;
        }

        private void toolStripMenuItemB_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFilesB?.ToList();
            if (items == null) return;
            foreach (var i in listBox_B.SelectedIndices.OfType<int>().Reverse())
            {
                items.RemoveAt(i);
            }
            SelectedRemoteFilesB = items;
        }

        private void listBox_remote_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.A | Keys.Control))
            {
                var listbox = sender as ListBox;
                listbox.BeginUpdate();
                try
                {
                    for (var i = 0; i < listbox.Items.Count; i++)
                        listbox.SetSelected(i, true);
                }
                finally
                {
                    listbox.EndUpdate();
                }
            }
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            runningJob?.Cancel();
        }

        private void listBox_remote_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private IEnumerable<IRemoteItem> GetSelectedItemsFromDataObject(System.Windows.Forms.IDataObject data)
        {
            object ret = null;
            FORMATETC fmt = new FORMATETC { cfFormat = ClipboardRemoteDrive.CF_CLOUD_DRIVE_ITEMS, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -2, ptd = IntPtr.Zero, tymed = TYMED.TYMED_ISTREAM };
            STGMEDIUM media = new STGMEDIUM();
            (data as System.Runtime.InteropServices.ComTypes.IDataObject).GetData(ref fmt, out media);
            var st = new IStreamWrapper(Marshal.GetTypedObjectForIUnknown(media.unionmember, typeof(IStream)) as IStream)
            {
                Position = 0
            };
            var bf = new BinaryFormatter();
            ret = bf.Deserialize(st);
            ClipboardRemoteDrive.ReleaseStgMedium(ref media);
            return (ret as string[]).Select(x => RemoteServerFactory.PathToItem(x));
        }

        private void listBox_remote_DragDrop(object sender, DragEventArgs e)
        {
            if(sender == listBox_A)
            {
                if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
                {
                    var selects = GetSelectedItemsFromDataObject(e.Data);
                    if (SelectedRemoteFilesA == null || SelectedRemoteFilesA.Count() == 0)
                        SelectedRemoteFilesA = selects;
                    else
                    {
                        SelectedRemoteFilesA = selects.Concat(SelectedRemoteFilesA);
                    }
                }
            }
            else if(sender == listBox_B)
            {
                if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
                {
                    var selects = GetSelectedItemsFromDataObject(e.Data);
                    if (SelectedRemoteFilesB == null || SelectedRemoteFilesB.Count() == 0)
                        SelectedRemoteFilesB = selects;
                    else
                    {
                        SelectedRemoteFilesB = selects.Concat(SelectedRemoteFilesB);
                    }
                }
            }
        }

        private void button_clearRemoteA_Click(object sender, EventArgs e)
        {
            SelectedRemoteFilesA = null;
            listBox_A.DataSource = null;
        }

        private void button_clearRemoteB_Click(object sender, EventArgs e)
        {
            SelectedRemoteFilesB = null;
            listBox_B.DataSource = null;
        }

    }
}
