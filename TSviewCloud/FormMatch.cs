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
    public sealed partial class FormMatch : Form
    {
        private static readonly FormMatch _instance = new FormMatch();

        public static FormMatch Instance
        {
            get
            {
                return _instance;
            }
        }

        private FormMatch()
        {
            InitializeComponent();
            listBox_remote.DataSource = _SelectedRemoteFiles;
        }

        private EventHandler _addCallback;
        private IEnumerable<IRemoteItem> _SelectedRemoteFiles;
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
                    var item = RemoteServerFactory.PathToItem(x.FullPath).Result;
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

        public IEnumerable<IRemoteItem> SelectedRemoteFiles
        {
            get
            {
                return _SelectedRemoteFiles;
            }
            set
            {
                if (value == null)
                {
                    _SelectedRemoteFiles = null;
                    listBox_remote.DataSource = null;
                }
                else
                {
                    Cursor.Current = Cursors.WaitCursor;
                    _SelectedRemoteFiles = value.ToArray()
                        .AsParallel()
                        .Select(x => GetItems(RemoteServerFactory.PathToItem(x.FullPath).Result))
                        .SelectMany(x => x.Select(y => y))
                        .Distinct()
                        .Where(x => x.ItemType == RemoteItemType.File);
                    listBox_remote.DataSource = _SelectedRemoteFiles.ToList();
                }
            }
        }

        public EventHandler AddCallback { get => _addCallback; set => _addCallback = value; }

        private void button_AddFile_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            Cursor.Current = Cursors.WaitCursor;
            listBox_local.Items.AddRange(openFileDialog1.FileNames.Where(x => listBox_local.Items.IndexOf(x) < 0).ToArray());
        }

        private void DoDirectoryAdd(IEnumerable<string> DirNames)
        {
            foreach (var filename in DirNames)
            {
                listBox_local.Items.AddRange(Directory.EnumerateFiles(filename).Select(x => ItemControl.GetOrgFilename(x)).Where(x => listBox_local.Items.IndexOf(x) < 0).ToArray());

                DoDirectoryAdd(Directory.EnumerateDirectories(filename));
            }
        }

        private void button_AddFolder_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;

            label_info.Text = "Add Folder ...";
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                DoDirectoryAdd(new string[] { ItemControl.GetLongFilename(folderBrowserDialog1.SelectedPath) });
            }
            catch { }
            label_info.Text = "";
        }

        private void deltetItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(var i in listBox_local.SelectedIndices.OfType<int>().Reverse())
            {
                listBox_local.Items.RemoveAt(i);
            }
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.A | Keys.Control))
            {
                listBox_local.BeginUpdate();
                try
                {
                    for (var i = 0; i < listBox_local.Items.Count; i++)
                        listBox_local.SetSelected(i, true);
                }
                finally
                {
                    listBox_local.EndUpdate();
                }
            }
        }

        static public string GetBasePath(IEnumerable<string> paths)
        {
            string prefix = null;
            foreach(var p in paths)
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

        public class LocalItemInfo
        {
            public string path;
            public string name;
            public long size;
            public string Hash;
            public LocalItemInfo(string path, string name, long size, string Hash)
            {
                this.path = path;
                this.name = name;
                this.size = size;
                this.Hash = Hash;
            }
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
            public LocalItemInfo local;
            public RemoteItemInfo remote;
            public MatchItem(LocalItemInfo local, RemoteItemInfo remote)
            {
                this.local = local;
                this.remote = remote;
            }
        }

        enum HashType
        {
            None,
            MD5,
        };

        private void button_start_Click(object sender, EventArgs e)
        {
            if (SelectedRemoteFiles == null) return;

            ConcurrentBag<MatchItem> RemoteOnly = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> LocalOnly = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> BothAndMatch = new ConcurrentBag<MatchItem>();
            ConcurrentBag<MatchItem> BothAndUnmatch = new ConcurrentBag<MatchItem>();
            ConcurrentDictionary<string, LocalItemInfo[]> LocalDup = new ConcurrentDictionary<string, LocalItemInfo[]>();
            ConcurrentDictionary<string, RemoteItemInfo[]> RemoteDup = new ConcurrentDictionary<string, RemoteItemInfo[]>();

            var synchronizationContext = SynchronizationContext.Current;
            bool TreeFlag = radioButton_Tree.Checked;
            bool FilenameFlag = radioButton_filename.Checked;
            bool HashFlag = radioButton_Hash.Checked;
            HashType hashType = HashType.None;
            if (radioButton_HashMD5.Checked) hashType = HashType.MD5;

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

                var remote = SelectedRemoteFiles.Select(x => new RemoteItemInfo(x, x.FullPath, null)).ToArray();
                var remotebasepath = GetBasePathRemote(remote.Select(x => x.path));

                if (TreeFlag)
                    remote = remote.Select(x => new RemoteItemInfo(x.info, x.path, x.path.Substring(remotebasepath.Length))).ToArray();
                if (FilenameFlag)
                    remote = remote.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Name)).ToArray();
                if (HashFlag)
                    remote = remote.Select(x => new RemoteItemInfo(x.info, x.path, x.info.Hash ?? "")).ToArray();

                var localpath = listBox_local.Items.Cast<string>();
                var localbasepath = GetBasePath(localpath);
                var len = localpath.Count();
                int i = 0;
                foreach (var ritem in remote.GroupBy(x => x.name).Where(g => g.Count() > 1))
                {
                    RemoteDup[ritem.Key] = ritem.ToArray();
                }
                var local = localpath.AsParallel()
                .WithDegreeOfParallelism(Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)))
                .Select(x =>
                {
                    byte[] hash = null;
                    Interlocked.Increment(ref i);

                    System.Security.Cryptography.HashAlgorithm hashCalc = null;
                    switch (hashType)
                    {
                        case HashType.MD5:
                            hashCalc = new System.Security.Cryptography.MD5CryptoServiceProvider();
                            break;
                    }

                    synchronizationContext.Send((o) =>
                    {
                        if (runningJob?.Ct.IsCancellationRequested ?? true) return;
                        label_info.Text = o as string;
                    }, string.Format("{0}/{1} Check file {3}...{2}", i, len, x, (hashCalc == null) ? "" : "Hash"));

                    using (hashCalc)
                    using (var hfile = File.Open(ItemControl.GetLongFilename(x), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        hash = hashCalc?.ComputeHash(hfile);
                        var HASH = (hash != null) ? BitConverter.ToString(hash).ToLower().Replace("-", "") : null;

                        switch (hashType)
                        {
                            case HashType.MD5:
                                HASH = "MD5:" + HASH;
                                break;
                        }

                        if (HashFlag)
                            return new LocalItemInfo(x, HASH, hfile.Length, HASH);
                        if (TreeFlag)
                            return new LocalItemInfo(x, x.Substring(localbasepath.Length).Replace('\\', '/'), hfile.Length, HASH);
                        else
                            return new LocalItemInfo(x, Path.GetFileName(x), hfile.Length, HASH);
                    }
                }).GroupBy(x => x.name).ToArray();

                i = 0;
                Parallel.ForEach(local,
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (litem) =>
                    {
                        job.Ct.ThrowIfCancellationRequested();
                        var matchitem = remote.Where(x => x.name == litem.FirstOrDefault()?.name).ToArray();

                        if (litem.Count() > 1)
                        {
                            LocalDup[litem.Key] = litem.ToArray();
                        }

                        if (matchitem.Length > 0)
                        {
                            List<RemoteItemInfo> RemoteMatched = new List<RemoteItemInfo>();
                            List<LocalItemInfo> LocalUnMatched = new List<LocalItemInfo>();
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
                                    if (item.size == ritem.info.Size)
                                    {
                                        if (item.Hash == null || ritem.info.Hash == "" || item.Hash == ritem.info.Hash)
                                        {
                                            Matched.Add(ritem);
                                        }
                                    }
                                }

                                if (Matched.Count() == 0)
                                {
                                    LocalUnMatched.Add(item);
                                }


                                Parallel.ForEach(Matched.Select(x => new MatchItem(item, x)), (x) => BothAndMatch.Add(x));
                                RemoteMatched.AddRange(Matched);
                            }

                            var RemoteUnMatched = matchitem.Except(RemoteMatched);
                            if (RemoteUnMatched.Count() < LocalUnMatched.Count())
                            {
                                Parallel.ForEach(RemoteUnMatched.Concat(RemoteMatched).Zip(LocalUnMatched, (r, l) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
                            }
                            else if (RemoteUnMatched.Count() > LocalUnMatched.Count())
                            {
                                Parallel.ForEach(LocalUnMatched.Concat(litem).Zip(RemoteUnMatched, (l, r) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
                            }
                            else
                            {
                                if (RemoteUnMatched.Count() > 0)
                                    Parallel.ForEach(LocalUnMatched.Zip(RemoteUnMatched, (l, r) => new MatchItem(l, r)), (x) => BothAndUnmatch.Add(x));
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
                                LocalOnly.Add(new MatchItem(item, null));
                            }
                        }
                    });
                Parallel.ForEach(
                    remote.Select(x => x.info)
                    .Except(BothAndMatch.Select(x => x.remote.info))
                    .Except(BothAndUnmatch.Select(x => x.remote.info))
                    .Select(x => remote.Where(y => y.info == x).FirstOrDefault())
                    .Select(x => new MatchItem(null, x)),
                    new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                    (x) => RemoteOnly.Add(x));
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
                        var result = new FormMatchResult();
                        result.RemoteOnly = RemoteOnly.ToArray();
                        result.LocalOnly = LocalOnly.ToArray();
                        result.Unmatch = BothAndUnmatch.ToArray();
                        result.Match = BothAndMatch.ToArray();
                        result.RemoteDup = RemoteDup;
                        result.LocalDup = LocalDup;
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

        private void button_AddRemote_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFiles?.ToList();
            if (items == null)
            {
                AddCallback?.Invoke(this, new EventArgs());
            }
            else
            {
                AddCallback?.Invoke(this, new EventArgs());
                items.AddRange(SelectedRemoteFiles);
                SelectedRemoteFiles = items;
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var items = SelectedRemoteFiles?.ToList();
            if (items == null) return;
            foreach (var i in listBox_remote.SelectedIndices.OfType<int>().Reverse())
            {
                items.RemoveAt(i);
            }
            SelectedRemoteFiles = items;
        }

        private void listBox_remote_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.A | Keys.Control))
            {
                listBox_remote.BeginUpdate();
                try
                {
                    for (var i = 0; i < listBox_remote.Items.Count; i++)
                        listBox_remote.SetSelected(i, true);
                }
                finally
                {
                    listBox_remote.EndUpdate();
                }
            }
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            runningJob?.Cancel();
        }

        private void listBox_local_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void listBox_local_DragDrop(object sender, DragEventArgs e)
        {
            string[] fileName =
                (string[])e.Data.GetData(DataFormats.FileDrop, false);

            foreach (var item in fileName)
            {
                try
                {
                    if (File.GetAttributes(item).HasFlag(FileAttributes.Directory))
                        DoDirectoryAdd(new string[] { item });
                    else
                    {
                        if (listBox_local.Items.IndexOf(item) < 0)
                            listBox_local.Items.Add(item);
                    }
                }
                catch { }
            }
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
            return (ret as string[]).Select(x => RemoteServerFactory.PathToItem(x).Result);
        }

        private void listBox_remote_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
            {
                var selects = GetSelectedItemsFromDataObject(e.Data);
                if (SelectedRemoteFiles == null || SelectedRemoteFiles.Count() == 0)
                    SelectedRemoteFiles = selects;
                else
                {
                    SelectedRemoteFiles = selects.Concat(SelectedRemoteFiles);
                }
            }
        }

        private void button_clearLocal_Click(object sender, EventArgs e)
        {
            listBox_local.Items.Clear();
        }

        private void button_clearRemote_Click(object sender, EventArgs e)
        {
            SelectedRemoteFiles = null;
            listBox_remote.DataSource = null;
        }

        private void radioButton_HashNone_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton_HashNone.Checked)
            {
                if (radioButton_Hash.Checked)
                    radioButton_Tree.Checked = true;
                radioButton_Hash.Enabled = false;
            }
            else
            {
                radioButton_Hash.Enabled = true;
            }
        }
    }
}
