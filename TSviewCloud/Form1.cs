using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace TSviewCloud
{
    public partial class Form1 : Form
    {
        private bool _applicationExit = false;
        public bool ApplicationExit
        {
            get { return false; }
            private set
            {
                _applicationExit = value;
                TSviewCloudConfig.Config.ApplicationExit = value;
            }
        }

        bool Initialized = false;
        List<string> AddressLog = new List<string>();
        int AddressLogPtr = -1;
        bool AddressSelecting = false;
        bool AddressLogSelecting = false;

        public Form1()
        {
            InitializeComponent();
            listData = new RemoteListViewItemList(this);
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            TSviewCloud.FormClosing.Instance.IncShowCount();
            var loadJob = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.Normal);
            loadJob.DisplayName = "Load server list";
            TSviewCloudPlugin.JobControler.Run(loadJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                try
                {
                    TSviewCloudPlugin.RemoteServerFactory.Restore();

                    while (!TSviewCloudPlugin.RemoteServerFactory.ServerList.Values.All(x => x.IsReady))
                    {
                        loadJob.Ct.ThrowIfCancellationRequested();
                        Task.Delay(500).Wait(loadJob.Ct);
                    }
                }
                finally
                {
                    synchronizationContext.Post((o) =>
                    {
                        TSviewCloud.FormClosing.Instance.DecShowCount();
                    }, null);
                    Initialized = true;
                }

                j.Progress = 1;
                j.ProgressStr = "Done.";
            });



            var displayJob = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.LoadItem, depends: loadJob);
            displayJob.DisplayName = "Display  server";
            TSviewCloudPlugin.JobControler.Run(displayJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";

                synchronizationContext.Send((o) =>
                {
                    foreach (var server in TSviewCloudPlugin.RemoteServerFactory.ServerList.Values)
                    {
                        listView1.SmallImageList.Images.Add(server.Icon);
                        listView1.LargeImageList.Images.Add(server.Icon);

                        var root = new TreeNode(server.Name, treeView1.ImageList.Images.Count - 1, treeView1.ImageList.Images.Count - 1)
                        {
                            Name = server.Name
                        };
                        treeView1.Nodes.Add(root);

                        var item = new ToolStripMenuItem(server.Name, server.Icon.ToBitmap());
                        item.Click += DisconnectServer;
                        item.Tag = server;
                        disconnectToolStripMenuItem.DropDownItems.Add(item);
                    }

                    j.Progress = 1;
                    j.ProgressStr = "Done.";
                }, null);
            });

            var load2Job = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.ControlMaster);
            TSviewCloudPlugin.JobControler.Run(load2Job, (j) =>
            {
                displayJob.Wait(ct: j.Ct);
                var display2Job = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                    type: TSviewCloudPlugin.JobClass.LoadItem,
                    depends: TSviewCloudPlugin.RemoteServerFactory.ServerList.Values
                        .Select(s => TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(s.Name + "://")).ToArray()
                );
                display2Job.DisplayName = "Display  server";
                display2Job.ProgressStr = "wait for load";
                TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(display2Job, (j2j) =>
                {
                    displayJob.Wait(ct: j.Ct);
                    j2j.Progress = -1;
                    j2j.ProgressStr = "Loading...";
                    var results = new List<TSviewCloudPlugin.IRemoteItem>();
                    foreach (var item in j2j.ResultOfDepend)
                    {
                        if (item.TryGetTarget(out var result))
                        {
                            results.Add(result);
                        }
                    }

                    synchronizationContext.Post((o) =>
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        foreach (var item in o as IEnumerable<TSviewCloudPlugin.IRemoteItem>)
                        {
                            var treenode = treeView1.Nodes.Find(item.Server, false).First();
                            treenode.Tag = item;
                            ExpandItem(treenode);
                        }
                    }, results);
                });
            });
        }

        const int WM_CLOSE = 0x10;
        TSviewCloudPlugin.Job SaveConfigJob;

        private async void Form1_FormClosingAsync(object sender, FormClosingEventArgs e)
        {
            if (Initialized && SaveConfigJob == null)
            {
                SaveConfigJob = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.Save);
                SaveConfigJob.DisplayName = "Save server list";
                TSviewCloudPlugin.JobControler.Run(SaveConfigJob, (j) =>
                {
                    j.Progress = -1;
                    j.ProgressStr = "Save...";

                    TSviewCloudConfig.Config.Save();
                    TSviewCloudPlugin.RemoteServerFactory.Save();

                    j.Progress = 1;
                    j.ProgressStr = "Done.";
                });
            }

            if (TSviewCloudPlugin.JobControler.CancelAll())
            {
                e.Cancel = true;
                if (!TSviewCloud.FormClosing.Instance.Visible)
                {
                    TSviewCloud.FormClosing.Instance.Show();
                    Application.DoEvents();
                }
                await Task.Delay(100);
                PostMessage(Handle, WM_CLOSE, 0, 0);
            }
            else
            {
                ApplicationExit = true;
            }
        }



        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        [DllImport("User32.dll")]
        public static extern int PostMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// hi-DPI 
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////

        const int WM_DPICHANGED = 0x02E0;

        private bool needAdjust = false;
        private bool isMoving = false;
        int oldDpi;
        int currentDpi;

        protected override void OnResizeBegin(EventArgs e)
        {
            base.OnResizeBegin(e);
            isMoving = true;
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            isMoving = false;
            if (needAdjust)
            {
                needAdjust = false;
                HandleDpiChanged();
            }
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (needAdjust && IsLocationGood())
            {
                needAdjust = false;
                HandleDpiChanged();
            }
        }

        private bool IsLocationGood()
        {
            if (oldDpi == 0) return false;

            float scaleFactor = (float)currentDpi / oldDpi;

            int widthDiff = (int)(ClientSize.Width * scaleFactor) - ClientSize.Width;
            int heightDiff = (int)(ClientSize.Height * scaleFactor) - ClientSize.Height;

            var rect = new W32.RECT
            {
                left = Bounds.Left,
                top = Bounds.Top,
                right = Bounds.Right + widthDiff,
                bottom = Bounds.Bottom + heightDiff
            };

            var handleMonitor = W32.MonitorFromRect(ref rect, W32.MONITOR_DEFAULTTONULL);

            if (handleMonitor != IntPtr.Zero)
            {
                if (W32.GetDpiForMonitor(handleMonitor, W32.Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint dpiY) == 0)
                {
                    if (dpiX == currentDpi)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DPICHANGED:
                    oldDpi = currentDpi;
                    currentDpi = m.WParam.ToInt32() & 0xFFFF;

                    if (oldDpi != currentDpi)
                    {
                        if (isMoving)
                        {
                            needAdjust = true;
                        }
                        else
                        {
                            HandleDpiChanged();
                        }
                    }
                    else
                    {
                        needAdjust = false;
                    }
                    break;
            }

            base.WndProc(ref m);
        }


        private void HandleDpiChanged()
        {
            if (oldDpi != 0)
            {
                float scaleFactor = (float)currentDpi / oldDpi;

                //the default scaling method of the framework
                Scale(new SizeF(scaleFactor, scaleFactor));

                //fonts are not scaled automatically so we need to handle this manually
                ScaleFonts(scaleFactor);

                //perform any other scaling different than font or size (e.g. ItemHeight)
                PerformSpecialScaling(scaleFactor);
            }
        }

        protected virtual void PerformSpecialScaling(float scaleFactor)
        {
            foreach (ColumnHeader c in listView1.Columns)
            {
                c.Width = (int)(c.Width * scaleFactor);
            }
        }

        protected virtual void ScaleFonts(float scaleFactor)
        {
            Font = new Font(Font.FontFamily,
                   Font.Size * scaleFactor,
                   Font.Style);
            //ScaleFontForControl(this, scaleFactor);
        }

        private static void ScaleFontForControl(Control control, float factor)
        {
            control.Font = new Font(control.Font.FontFamily,
                   control.Font.Size * factor,
                   control.Font.Style);

            foreach (Control child in control.Controls)
            {
                ScaleFontForControl(child, factor);
            }
        }

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// virtual listview
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////

        enum ListColums
        {
            Name = 0,
            Size = 1,
            modifiedDate = 2,
            createdDate = 3,
            path = 4,
            Hash = 5,
        };

        class RemoteListViewItemList
        {
            Form1 owner;
            public RemoteListViewItemList(Form1 owner)
            {
                this.owner = owner;
            }

            List<TSviewCloudPlugin.IRemoteItem> remoteItemList = new List<TSviewCloudPlugin.IRemoteItem>();

            public void SetSearchResults(IEnumerable<TSviewCloudPlugin.IRemoteItem> results)
            {
                Clear();
                remoteItemList.Clear();
                remoteItemList.AddRange(results.OrderByDescending(x => x.ItemType).ThenBy(x => x.Name));
                CurrentViewItem = null;
            }

            TSviewCloudPlugin.IRemoteItem _currentViewItem;
            public TSviewCloudPlugin.IRemoteItem CurrentViewItem
            {
                get { return _currentViewItem; }
                set
                {
                    _currentViewItem = value;
                    IsSearchResult = false;
                    if (_currentViewItem != null)
                    {
                        IsSearchResult = false;
                        remoteItemList.Clear();
                        if (_currentViewItem.Children != null)
                            remoteItemList.AddRange(_currentViewItem.Children.OrderByDescending(x => x.ItemType).ThenBy(x => x.Name));
                    }
                    else
                    {
                        IsSearchResult = true;
                    }

                    foreach (int i in owner.listView1.SelectedIndices)
                    {
                        owner.listView1.Items[i].Selected = false;
                    }
                    owner.listView1.VirtualListSize = Count;
                    owner.listView1.Invalidate();
                }
            }
            public TSviewCloudPlugin.IRemoteItem ParentViewItem
            {
                get { return _currentViewItem?.Parents?.FirstOrDefault(); }
            }
            public TSviewCloudPlugin.IRemoteItem this[int index]
            {
                get
                {
                    if (CurrentViewItem == null)
                    {
                        return remoteItemList[index];
                    }
                    else
                    {
                        if (index == 0)
                        {
                            return CurrentViewItem;
                        }
                        else if (index == 1)
                        {
                            return ParentViewItem;
                        }
                        else
                        {
                            return remoteItemList[index - 2];
                        }
                    }
                }
            }
            public IEnumerable<TSviewCloudPlugin.IRemoteItem> Items
            {
                get
                {
                    if (CurrentViewItem == null) return remoteItemList;
                    var ret = new List<TSviewCloudPlugin.IRemoteItem>
                    {
                        CurrentViewItem,
                        ParentViewItem
                    };
                    ret.AddRange(remoteItemList);
                    return ret;
                }
                set
                {
                    remoteItemList.Clear();
                    remoteItemList.AddRange(value);
                    CurrentViewItem = null;
                    owner.listView1.VirtualListSize = Count;
                }
            }
            public bool IsSpetialItem(int index)
            {
                if (IsSearchResult)
                {
                    return false;
                }
                else
                {
                    return index < 2;
                }
            }

            private ListColums _SortColum = ListColums.Name;
            private SortOrder _SortOrder = System.Windows.Forms.SortOrder.Ascending;
            private bool _SortKind = false;

            private Func<IEnumerable<TSviewCloudPlugin.IRemoteItem>, IOrderedEnumerable<TSviewCloudPlugin.IRemoteItem>> SortFunction
            {
                get
                {
                    if (_SortOrder != SortOrder.Descending)
                    {
                        switch (_SortColum)
                        {
                            case ListColums.Name:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.Name);
                            case ListColums.Size:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.Size ?? 0);
                            case ListColums.modifiedDate:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.ModifiedDate);
                            case ListColums.createdDate:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.CreatedDate);
                            case ListColums.path:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.Server + "://" + Uri.UnescapeDataString(y.Path));
                            case ListColums.Hash:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.Hash ?? "");
                            default:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenBy(y => y.Name);
                        }
                    }
                    else
                    {
                        switch (_SortColum)
                        {
                            case ListColums.Name:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.Name);
                            case ListColums.Size:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.Size ?? 0);
                            case ListColums.modifiedDate:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.ModifiedDate);
                            case ListColums.createdDate:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.CreatedDate);
                            case ListColums.path:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.Server + "://" + Uri.UnescapeDataString(y.Path));
                            case ListColums.Hash:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.Hash ?? "");
                            default:
                                return (IEnumerable<TSviewCloudPlugin.IRemoteItem> x) => x.SortByKind(_SortKind).ThenByDescending(y => y.Name);
                        }
                    }
                }
            }

            private void Sort()
            {
                remoteItemList = SortFunction(remoteItemList).ToList();
                owner.listView1.Invalidate();
                foreach (ToolStripMenuItem mitem in owner.sortToolStripMenuItem.DropDownItems)
                {
                    if ((mitem.Tag as ListColums?).Value == _SortColum) mitem.Checked = true;
                    else mitem.Checked = false;
                }
                owner.sortBytypeToolStripMenuItem.Checked = _SortKind;
            }

            public ListColums SortColum
            {
                get { return _SortColum; }
                set
                {
                    if (_SortColum == value)
                    {
                        SortOrder = (SortOrder == SortOrder.Ascending) ? SortOrder.Descending : SortOrder.Ascending;
                    }
                    else
                    {
                        _SortColum = value;
                        SortOrder = SortOrder.Ascending;
                    }
                    Sort();
                }
            }
            public SortOrder SortOrder
            {
                get { return _SortOrder; }
                set
                {
                    _SortOrder = value;
                    Sort();
                }
            }
            public bool SortKind
            {
                get { return _SortKind; }
                set
                {
                    _SortKind = value;
                    Sort();
                }
            }
            public bool IsSearchResult
            {
                get; private set;
            }


            public IEnumerable<TSviewCloudPlugin.IRemoteItem> GetItems(ListView.SelectedIndexCollection indices, bool IncludeSpetial = true)
            {
                List<TSviewCloudPlugin.IRemoteItem> ret = new List<TSviewCloudPlugin.IRemoteItem>();
                foreach (int i in indices)
                {
                    if (IsSearchResult)
                    {
                        if (i >= 0 && i < remoteItemList.Count) ret.Add(remoteItemList[i]);
                    }
                    else
                    {
                        if (i == 0)
                        {
                            if (IncludeSpetial) ret.Add(CurrentViewItem);
                        }
                        else if (i == 1)
                        {
                            if (IncludeSpetial) ret.Add(ParentViewItem);
                        }
                        else if (i >= 2 && i - 2 < remoteItemList.Count) ret.Add(remoteItemList[i - 2]);
                    }
                }
                return ret;
            }

            public void Clear()
            {
                CurrentViewItem = null;
                remoteItemList.Clear();
                owner.listView1.VirtualListSize = Count;
            }

            public int Count
            {
                get { return (CurrentViewItem == null) ? remoteItemList.Count : remoteItemList.Count + 2; }
            }


            private ListViewItem ConvertNormalItem(TSviewCloudPlugin.IRemoteItem item)
            {
                var listitem = new ListViewItem();
                listitem.Text = item.Name;
                listitem.ImageIndex = (item.ItemType == TSviewCloudPlugin.RemoteItemType.Folder) ? 1 : 0;
                listitem.SubItems.Add(item.Size?.ToString("N0"));
                listitem.SubItems.Add(item.ModifiedDate.ToString());
                listitem.SubItems.Add(item.CreatedDate.ToString());
                listitem.SubItems.Add(item.FullPath);
                listitem.SubItems.Add(item.Hash);
                listitem.Tag = item;
                listitem.ToolTipText = item.Name;
                return listitem;
            }

            public ListViewItem GetListViewItem(int index)
            {
                var listitem = new ListViewItem(new string[6]);
                TSviewCloudPlugin.IRemoteItem item;
                if (IsSearchResult)
                {
                    if (index < remoteItemList.Count)
                        return ConvertNormalItem(remoteItemList[index]);
                    else
                        return new ListViewItem(new string[6]);
                }
                else
                {
                    if (index == 0)
                    {
                        item = CurrentViewItem;
                        listitem.Text = ".";
                        listitem.ImageIndex = 1;
                    }
                    else if (index == 1)
                    {
                        item = ParentViewItem;
                        listitem.Text = "..";
                        listitem.ImageIndex = 1;
                    }
                    else if (index > 1 && index - 2 < remoteItemList.Count)
                        return ConvertNormalItem(remoteItemList[index - 2]);
                    else
                        return new ListViewItem(new string[6]);
                }
                listitem.SubItems.Add(item?.Size?.ToString("N0"));
                listitem.SubItems.Add(item?.ModifiedDate.ToString());
                listitem.SubItems.Add(item?.CreatedDate.ToString());
                listitem.SubItems.Add(item?.FullPath);
                listitem.SubItems.Add(item?.Hash);
                listitem.Tag = item;
                return listitem;
            }
            public bool Contains(string path)
            {
                return (CurrentViewItem?.FullPath == path) || (ParentViewItem?.FullPath == path) || (remoteItemList.Select(x => x.FullPath).Contains(path));
            }
            public bool Contains(IEnumerable<string> id)
            {
                return id.Select(x => Contains(x)).Any();
            }
        }

        RemoteListViewItemList listData;
        private SynchronizationContext synchronizationContext;

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// functions
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox1().ShowDialog();
        }

        private void outputLogTofileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            outputLogTofileToolStripMenuItem.Checked = !outputLogTofileToolStripMenuItem.Checked;
            TSviewCloudConfig.Config.LogToFile = outputLogTofileToolStripMenuItem.Checked;
        }

        private void ConnectServer(object sender, EventArgs e)
        {
            var f = new FormNewServer
            {
                ServerName = (sender as ToolStripMenuItem).Tag as string
            };
            if (f.ShowDialog() == DialogResult.OK)
            {
                var server = f.Target;
                listView1.SmallImageList.Images.Add(server.Icon);
                listView1.LargeImageList.Images.Add(server.Icon);

                var root = new TreeNode(server.Name, treeView1.ImageList.Images.Count - 1, treeView1.ImageList.Images.Count - 1)
                {
                    Name = server.Name,
                    Tag = server[""]
                };
                ExpandItem(root);
                treeView1.Nodes.Add(root);

                var item = new ToolStripMenuItem(server.Name, server.Icon.ToBitmap());
                item.Click += DisconnectServer;
                item.Tag = server;
                disconnectToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void DisconnectServer(object sender, EventArgs e)
        {
            var server = (sender as ToolStripMenuItem).Tag as TSviewCloudPlugin.IRemoteServer;
            if (MessageBox.Show(string.Format("Do you want disconnect from server '{0}'", server.Name), "Disconnect", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (listData.CurrentViewItem?.Server == server.Name)
                {
                    listData.Clear();
                }
                treeView1.Nodes.RemoveByKey(server.Name);
                TSviewCloudPlugin.RemoteServerFactory.Delete(server);
                disconnectToolStripMenuItem.DropDownItems.Remove(sender as ToolStripMenuItem);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            synchronizationContext = SynchronizationContext.Current;
            TSviewCloudPlugin.ItemControl.synchronizationContext = SynchronizationContext.Current;

            if (!TSviewCloudConfig.Config.IsMasterPasswordCorrect)
            {
                using (var f = new FormMasterPass())
                    f.ShowDialog();
                if (!TSviewCloudConfig.Config.IsMasterPasswordCorrect)
                {
                    Close();
                    return;
                }
            }

            outputLogTofileToolStripMenuItem.Checked = TSviewCloudConfig.Config.LogToFile;

            foreach (var dll in TSviewCloudPlugin.RemoteServerFactory.DllList)
            {
                var plugin = TSviewCloudPlugin.RemoteServerFactory.Get(dll.Key, null);
                var item = new ToolStripMenuItem(plugin.ServiceName, plugin.Icon.ToBitmap());
                item.Click += ConnectServer;
                item.Tag = dll.Key;
                connectToolStripMenuItem.DropDownItems.Add(item);
            }
            int w1, w2;
            using (var g = CreateGraphics())
            {
                currentDpi = (int)g.DpiY;
                w1 = (int)(g.MeasureString("9,999,999,999,999", listView1.Font).Width + 20.5);
                w2 = (int)(g.MeasureString("9999/99/99 99:99:99", listView1.Font).Width + 20.5);
            }
            listView1.Columns.Add("Name", 400).Tag = ListColums.Name;
            listView1.Columns.Add("Size", w1, HorizontalAlignment.Right).Tag = ListColums.Size;
            listView1.Columns.Add("ModifiedDate", w2).Tag = ListColums.modifiedDate;
            listView1.Columns.Add("CreatedDate", w2).Tag = ListColums.createdDate;
            listView1.Columns.Add("FullPath", 300).Tag = ListColums.path;
            listView1.Columns.Add("Hash", 200).Tag = ListColums.Hash;

            nameToolStripMenuItem.Tag = ListColums.Name;
            sizeToolStripMenuItem.Tag = ListColums.Size;
            modifiedDateToolStripMenuItem.Tag = ListColums.modifiedDate;
            createDateToolStripMenuItem.Tag = ListColums.createdDate;
            pathToolStripMenuItem.Tag = ListColums.path;
            hashToolStripMenuItem.Tag = ListColums.Hash;

            listData.SortKind = true;

            var smallimagelist = new ImageList();
            smallimagelist.Images.Add(Properties.Resources.File);
            smallimagelist.Images.Add(Properties.Resources.Folder);
            var largeimagelist = new ImageList();
            largeimagelist.ImageSize = new Size(64, 64);
            largeimagelist.ColorDepth = ColorDepth.Depth32Bit;
            largeimagelist.Images.Add(Properties.Resources.File);
            largeimagelist.Images.Add(Properties.Resources.Folder);
            treeView1.ImageList = smallimagelist;
            listView1.SmallImageList = smallimagelist;
            listView1.LargeImageList = largeimagelist;
        }

        private TSviewCloudPlugin.Job<TSviewCloudPlugin.IRemoteItem> ExpandItem(TreeNode baseNode)
        {
            var pitem = baseNode?.Tag as TSviewCloudPlugin.IRemoteItem;
            if (pitem != null)
            {
                var loadjob = TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(pitem.FullPath, TSviewCloudPlugin.ReloadType.Cache);

                var DisplayJob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(TSviewCloudPlugin.JobClass.LoadItem, depends: loadjob);
                DisplayJob.DisplayName = "Display  " + pitem.FullPath;
                TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(DisplayJob, (j) =>
                {
                    j.Progress = -1;
                    j.ProgressStr = "Loading...";
                    j.ForceHidden = true;

                    var result = j.ResultOfDepend[0];
                    if (!result.TryGetTarget(out var item))
                    {
                        j.ProgressStr = "done.";
                        j.Progress = 1;
                        return;
                    }

                    j.Result = item;

                    synchronizationContext.Send(async (o) =>
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        try
                        {
                            baseNode.Nodes.AddRange(
                                (await GenerateTreeNode(o as IEnumerable<TSviewCloudPlugin.IRemoteItem>))
                                .OrderByDescending(x => (x.Tag as TSviewCloudPlugin.IRemoteItem).ItemType)
                                .ThenBy(x => (x.Tag as TSviewCloudPlugin.IRemoteItem).Name)
                                .ToArray()
                            );
                        }
                        finally
                        {
                            j.ProgressStr = "done.";
                            j.Progress = 1;
                        }
                    }, item?.Children);
                });
                return DisplayJob;
            }
            return null;
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null) return;

            var joblist = new List<TSviewCloudPlugin.Job<TSviewCloudPlugin.IRemoteItem>>();
            foreach (TreeNode item in e.Node.Nodes)
            {
                if (item.Nodes.Count > 0) continue;
                joblist.Add(ExpandItem(item));
            }
            if (joblist.Count == 0) return;

            TSviewCloud.FormClosing.Instance.IncShowCount();
            var finishjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(TSviewCloudPlugin.JobClass.Clean, depends: joblist.ToArray());
            TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(finishjob, (j) =>
            {
                synchronizationContext.Send((o) =>
                {
                    TSviewCloud.FormClosing.Instance.DecShowCount();
                }, j);
            });
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null) return;
            if (AddressSelecting) return;
            if (supressListviewRefresh) return;

            var path = (e.Node.Tag as TSviewCloudPlugin.IRemoteItem).FullPath;
            textBox_address.Text = path;

            TSviewCloud.FormClosing.Instance.IncShowCount();
            var loadjob = TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(path);
            var DisplayJob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(TSviewCloudPlugin.JobClass.LoadItem, depends: loadjob);
            DisplayJob.DisplayName = "Display  " + path;
            TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(DisplayJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                j.ForceHidden = true;

                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var dispitem))
                {
                    synchronizationContext.Send((o) =>
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        try
                        {
                            var item = o as TSviewCloudPlugin.IRemoteItem;
                            if (item?.ItemType == TSviewCloudPlugin.RemoteItemType.Folder)
                                ShowListViewItem(item);
                            else
                                ShowListViewItem(item?.Parents?.FirstOrDefault());
                        }
                        finally
                        {
                            j.ProgressStr = "done.";
                            j.Progress = 1;
                            TSviewCloud.FormClosing.Instance.DecShowCount();
                        }
                    }, dispitem);
                }
                else
                {
                    TSviewCloud.FormClosing.Instance.DecShowCount();
                }
            });
        }

        private void ShowListViewItem(TSviewCloudPlugin.IRemoteItem item)
        {
            listData.CurrentViewItem = item;
            if (item == null) return;

            textBox_address.Text = item.FullPath;
            if (AddressSelecting) return;
            if (AddressLogSelecting) return;
            if (AddressLog.Count - 1 > AddressLogPtr)
            {
                AddressLog.RemoveRange(AddressLogPtr + 1, AddressLog.Count - AddressLogPtr - 1);
            }
            AddressLog.Add(listData.CurrentViewItem.FullPath);
            AddressLogPtr++;
        }

        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = listData.GetListViewItem(e.ItemIndex);
        }

        private void largeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.LargeIcon;
        }

        private void smallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.SmallIcon;
        }

        private void detailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.Details;
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.List;
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                int ind = listView1.SelectedIndices[0];
                var item = listData[ind];
                if (item.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

                GotoAddress(item.FullPath);

                foreach (int i in listView1.SelectedIndices)
                {
                    listView1.Items[i].Selected = false;
                }
            }
        }


        private void GotoAddress(string path, TSviewCloudPlugin.ReloadType reload = TSviewCloudPlugin.ReloadType.Cache)
        {
            var m = Regex.Match(path, @"^(?<server>[^:]+)(://)(?<path>.*)$");
            TSviewCloudPlugin.Job loadjob;
            if (m.Success)
            {
                listData.Clear();
                loadjob = TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(path, reload);
            }
            else
            {
                var current = listData.CurrentViewItem;
                if (current == null) return;

                listData.Clear();
                loadjob = TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(current.FullPath, path, reload);
            }

            AddressSelecting = true;
            TSviewCloud.FormClosing.Instance.IncShowCount();
            var DisplayJob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(TSviewCloudPlugin.JobClass.LoadItem, depends: loadjob);
            DisplayJob.DisplayName = "Display  " + path;
            TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(DisplayJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                //DisplayJob.ForceHidden = true;

                var result = j.ResultOfDepend[0];
                if (!result.TryGetTarget(out var target))
                {
                    target = null;
                }
                j.Result = target;
                if (target == null)
                {
                    j.ProgressStr = "done.";
                    j.Progress = 1;
                    AddressSelecting = false;
                    TSviewCloud.FormClosing.Instance.DecShowCount();
                    return;
                }

                var pathlist = TSviewCloudPlugin.RemoteServerFactory.PathToItemChain(target.FullPath);
                var server = target.Server;

                synchronizationContext.Send(async (o) =>
                {
                    Cursor.Current = Cursors.WaitCursor;
                    try
                    {
                        ShowListViewItem(target);

                        var nodechain = new List<TreeNode>();
                        var node = treeView1.Nodes.Find(server, false).FirstOrDefault();
                        nodechain.Add(node);
                        foreach (var p in pathlist.Skip(1))
                        {
                            node = node.Nodes.Find(p.Name, false).FirstOrDefault();
                            if (node == null) break;
                            nodechain.Add(node);
                        }

                        int same_i = -1;
                        foreach (var p in pathlist.Select((p, i) => new { p, i }))
                        {
                            if (p.i >= nodechain.Count) break;
                            node = nodechain[p.i];
                            if (!p.p.Children?.Select(x => x.Name).OrderBy(x => x).SequenceEqual(node.Nodes.Cast<TreeNode>().Select(x => x.Name).OrderBy(x => x)) ?? true)
                                break;
                            same_i = p.i;
                        }

                        TSviewCloudPlugin.IRemoteItem item;
                        treeView1.BeginUpdate();
                        treeView1.CollapseAll();
                        try
                        {
                            var i = 0;
                            node = nodechain[0];
                            if (same_i < 0)
                            {
                                var p = pathlist[0];
                                var newnode = new TreeNode(p.Name)
                                {
                                    Name = p.Name,
                                    Tag = p
                                };
                                if (p.ItemType == TSviewCloudPlugin.RemoteItemType.Folder && p.Children?.Count() != 0)
                                {
                                    newnode.Nodes.AddRange(
                                        (await GenerateTreeNode(p.Children, 1))
                                            .OrderByDescending(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).ItemType)
                                            .ThenBy(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).Name)
                                            .ToArray());
                                }
                                foreach (TreeNode oldnode in node.Nodes)
                                {
                                    int ind = newnode.Nodes.IndexOfKey(oldnode.Name);
                                    if (ind < 0) continue;
                                    if (pathlist.Length > 1 && (pathlist[1].ItemType == TSviewCloudPlugin.RemoteItemType.File || pathlist[1].Name != oldnode.Name))
                                    {
                                        newnode.Nodes.RemoveAt(ind);
                                        newnode.Nodes.Insert(ind, oldnode.Clone() as TreeNode);
                                    }
                                }
                                node.Nodes.Clear();
                                node.Nodes.AddRange(newnode.Nodes.Cast<TreeNode>().ToArray());
                            }
                            foreach (var p in pathlist.Skip(1))
                            {
                                if (i++ >= same_i)
                                {
                                    int img = (p.ItemType == TSviewCloudPlugin.RemoteItemType.File) ? 0 : 1;
                                    var newnode = new TreeNode(p.Name, img, img)
                                    {
                                        Name = p.Name,
                                        Tag = p
                                    };
                                    if (p.ItemType == TSviewCloudPlugin.RemoteItemType.Folder && p.Children?.Count() != 0)
                                    {
                                        newnode.Nodes.AddRange(
                                            (await GenerateTreeNode(p.Children, 0))
                                                .OrderByDescending(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).ItemType)
                                                .ThenBy(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).Name)
                                                .ToArray());
                                    }
                                    foreach (TreeNode oldnode in node.Nodes.Find(p.Name, false).FirstOrDefault()?.Nodes)
                                    {
                                        if ((oldnode.Tag as TSviewCloudPlugin.IRemoteItem).ItemType == TSviewCloudPlugin.RemoteItemType.File)
                                            continue;

                                        int ind = newnode.Nodes.IndexOfKey(oldnode.Name);
                                        if (ind < 0) continue;
                                        newnode.Nodes.RemoveAt(ind);
                                        newnode.Nodes.Insert(ind, oldnode.Clone() as TreeNode);
                                    }

                                    int ind2 = node.Nodes.IndexOfKey(p.Name);
                                    node.Nodes.RemoveAt(ind2);
                                    node.Nodes.Insert(ind2, newnode);

                                    node.Expand();
                                    node = newnode;
                                }
                                else
                                {
                                    node.Expand();
                                    node = nodechain[i];
                                }
                            }
                            node.Expand();
                            treeView1.SelectedNode = node;
                            item = node?.Tag as TSviewCloudPlugin.IRemoteItem;
                        }
                        finally
                        {
                            treeView1.EndUpdate();
                            treeView1.SelectedNode?.EnsureVisible();
                            AddressSelecting = false;
                        }
                    }
                    finally
                    {
                        TSviewCloud.FormClosing.Instance.DecShowCount();
                        j.ProgressStr = "done.";
                        j.Progress = 1;
                    }
                }, null);
            });
        }

        private void button_addressGO_Click(object sender, EventArgs e)
        {
            GotoAddress(textBox_address.Text);
        }


        private void button_home_Click(object sender, EventArgs e)
        {
            GotoAddress("/");
        }

        private void button_up_Click(object sender, EventArgs e)
        {
            GotoAddress("..");
        }

        private void AddressItem_Click(object sender, EventArgs e)
        {
            AddressLogSelecting = true;
            GotoAddress((sender as ToolStripItem).Text);
            AddressLogPtr = ((sender as ToolStripItem).Tag as int?).Value;
            AddressLogSelecting = false;
        }

        private void button_prev_Click(object sender, EventArgs e)
        {
            contextMenuStrip_address.Items.Clear();
            for (var i = AddressLogPtr - 1; i >= 0 && i >= AddressLogPtr - 20; i--)
            {
                var item = contextMenuStrip_address.Items.Add(AddressLog[i]);
                item.Click += AddressItem_Click;
                item.Tag = i;
            }

            contextMenuStrip_address.Show(button_prev, new Point(0, button_prev.Height));
        }

        private void button_next_Click(object sender, EventArgs e)
        {
            contextMenuStrip_address.Items.Clear();
            for (var i = AddressLogPtr + 1; i >= 0 && i < AddressLog.Count && i < AddressLogPtr + 20; i++)
            {
                var item = contextMenuStrip_address.Items.Add(AddressLog[i]);
                item.Click += AddressItem_Click;
                item.Tag = i;
            }

            contextMenuStrip_address.Show(button_next, new Point(0, button_next.Height));
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            listData.SortColum = (listView1.Columns[e.Column].Tag as ListColums?).Value;
        }

        private void sortBytypeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listData.SortKind = !sortBytypeToolStripMenuItem.Checked;
        }

        private void SortKindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            listData.SortColum = (item.Tag as ListColums?).Value;
        }

        private async Task<IEnumerable<TreeNode>> GenerateTreeNode(IEnumerable<TSviewCloudPlugin.IRemoteItem> children, int count = 0)
        {
            var ret = new List<TreeNode>();
            if (children == null) return ret;
            return await Task.Run(() =>
            {
                Parallel.ForEach(children, () => new List<TreeNode>(), (x, state, local) =>
                {
                    int img = (x.ItemType == TSviewCloudPlugin.RemoteItemType.File) ? 0 : 1;
                    var node = new TreeNode(x.Name, img, img)
                    {
                        Name = x.Name,
                        Tag = x
                    };
                    if (x.ItemType == TSviewCloudPlugin.RemoteItemType.Folder && count > 0 && x.Children?.Count() != 0)
                    {
                        node.Nodes.AddRange(
                            GenerateTreeNode(x.Children, count - 1).Result
                                .OrderByDescending(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).ItemType)
                                .ThenBy(y => (y.Tag as TSviewCloudPlugin.IRemoteItem).Name)
                                .ToArray());
                    }
                    local.Add(node);
                    return local;
                },
                (result) =>
                {
                    lock (ret)
                        ret.AddRange(result);
                }
                );
                return ret;
            });
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listData.CurrentViewItem == null) return;

            GotoAddress(listData.CurrentViewItem.FullPath, TSviewCloudPlugin.ReloadType.Reload);
        }

        private void generalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new FormConfigEdit()).ShowDialog(this);
        }

        private void fFplayerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new FormConfigEdit() { SelectedTabpage = 1 }).ShowDialog(this);
        }

        private void logWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TSviewCloudConfig.Config.Log.Show();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            TSviewCloud.FormClosing.Instance.Active = false;
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            TSviewCloud.FormClosing.Instance.Active = false;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////
        /// 
        /// play files with ffmodule(FFmpeg)
        /// 
        ////////////////////////////////////////////////////////////////////////////////////////////////

        dynamic ffplayer = null;

        private void PlayWithFFmpeg(IEnumerable<TSviewCloudPlugin.IRemoteItem> serectedItem)
        {

            var asm = Assembly.LoadFrom("ffmodule.dll");
            var typeInfo = asm.GetType("ffmodule.FFplayer");

            dynamic Player = Activator.CreateInstance(typeInfo);
            var logger = Stream.Synchronized(new LogWindowStream(TSviewCloudConfig.Config.Log));
            var logwriter = TextWriter.Synchronized(new StreamWriter(logger));

            ffplayer = Player;
            Player.GetImageFunc = new ffmodule.GetImageDelegate(GetImage);
            Player.SetLogger(logwriter);
            Player.FontPath = TSviewCloudConfig.ConfigFFplayer.FontFilePath;
            Player.FontSize = TSviewCloudConfig.ConfigFFplayer.FontPtSize;
            Player.ScreenAuto = TSviewCloudConfig.ConfigFFplayer.AutoResize;
            Player.SetKeyFunctions(TSviewCloudConfig.ConfigFFplayer.FFmoduleKeybinds.Cast<dynamic>().ToDictionary(entry => (ffmodule.FFplayerKeymapFunction)entry.Key, entry => ((TSviewCloudConfig.FFmoduleKeysClass)entry.Value).Cast<Keys>().ToArray()));

            Player.Fullscreen = TSviewCloudConfig.ConfigFFplayer.fullscreen;
            Player.Display = TSviewCloudConfig.ConfigFFplayer.display;
            Player.Volume = TSviewCloudConfig.ConfigFFplayer.volume;
            Player.Mute = TSviewCloudConfig.ConfigFFplayer.mute;
            Player.ScreenWidth = TSviewCloudConfig.ConfigFFplayer.width;
            Player.ScreenHeight = TSviewCloudConfig.ConfigFFplayer.hight;
            Player.ScreenXPos = TSviewCloudConfig.ConfigFFplayer.x;
            Player.ScreenYPos = TSviewCloudConfig.ConfigFFplayer.y;


            var job = PlayFiles(serectedItem, new PlayOneFileDelegate(PlayOneFFmpegPlayer), "FFmpeg", data: Player);

            if (job == null) return;
            (job as TSviewCloudPlugin.Job).DoAlways = true;
            TSviewCloudPlugin.JobControler.Run(job as TSviewCloudPlugin.Job, (j) =>
            {
                Player.SetLogger(null);

                TSviewCloudConfig.ConfigFFplayer.fullscreen = Player.Fullscreen;
                TSviewCloudConfig.ConfigFFplayer.display = Player.Display;
                TSviewCloudConfig.ConfigFFplayer.mute = Player.Mute;
                TSviewCloudConfig.ConfigFFplayer.volume = Player.Volume;
                TSviewCloudConfig.ConfigFFplayer.width = Player.ScreenWidth;
                TSviewCloudConfig.ConfigFFplayer.hight = Player.ScreenHeight;
                TSviewCloudConfig.ConfigFFplayer.x = Player.ScreenXPos;
                TSviewCloudConfig.ConfigFFplayer.y = Player.ScreenYPos;

                ffplayer = null;

                j.Progress = 1;
                j.ProgressStr = "done.";
            });
        }

        private Bitmap GetImage(dynamic player)
        {
            try
            {
                var asm = Assembly.Load("ffmodule");
                var typeInfo = asm.GetType("ffmodule.FFplayer");

                Bitmap ret = null;
                var downitem = player.Tag as TSviewCloudPlugin.IRemoteItem;
                ImageCodecInfo[] decoders = ImageCodecInfo.GetImageDecoders();
                string filename = downitem.Name;
                var target = downitem.Parents?.FirstOrDefault()?.Children?.Where(x => x.Name.StartsWith(Path.GetFileNameWithoutExtension(filename)));

                var wjob = new TSviewCloudPlugin.Job(player.ct);

                var jobs = new List<TSviewCloudPlugin.Job<Stream>>();

                foreach (var t in target)
                {
                    var ext = Path.GetExtension(t.Name).ToLower();
                    foreach (var ici in decoders)
                    {
                        var decext = ici.FilenameExtension.Split(';').Select(x => Path.GetExtension(x).ToLower()).ToArray();
                        if (decext.Contains(ext))
                        {
                            var download = t.DownloadItemJob(prevJob: wjob);
                            jobs.Add(download);
                        }
                    }
                }
                var waitjob = TSviewCloudPlugin.JobControler.CreateNewJob<Stream>(TSviewCloudPlugin.JobClass.Normal, depends: jobs.ToArray());
                waitjob.DisplayName = "image loading";
                waitjob.ProgressStr = "wait for download";
                TSviewCloudPlugin.JobControler.Run<Stream>(waitjob, (j) =>
                {
                    j.Progress = -1;
                    bool found = false;
                    foreach (var result in j.ResultOfDepend)
                    {
                        if (result.TryGetTarget(out var d))
                        {
                            using (d)
                            {
                                if (found) continue;
                                try
                                {
                                    var img = Image.FromStream(d);
                                    ret = new Bitmap(img);
                                    found = true;
                                }
                                catch { }
                            }
                        }
                    }
                    j.Progress = 1;
                    j.ProgressStr = "done";
                });

                waitjob.Wait(ct: player.ct);
                return ret;
            }
            catch (Exception ex)
            {
                TSviewCloudConfig.Config.Log.LogOut(ex.ToString());
            }
            return null;
        }

        private void PlayOneFFmpegPlayer(TSviewCloudPlugin.IRemoteItem downitem, TSviewCloudPlugin.Job master, dynamic data)
        {
            string filename = downitem.Name;
            var Player = data;

            synchronizationContext.Post((o) =>
            {
                Player.StartSkip = PlayControler.StartDelay;
                Player.StopDuration = PlayControler.Duration;
            }, null);

            var download = downitem.DownloadItemJob(WeekDepend: true, prevJob: master);

            var job = TSviewCloudPlugin.JobControler.CreateNewJob<Stream>(TSviewCloudPlugin.JobClass.PlayDownload, depends: download);
            job.DisplayName = "Play :" + downitem.Name;
            job.ProgressStr = "wait for play";

            TSviewCloudPlugin.JobControler.Run<Stream>(job, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "now playing...";
                var result = j.ResultOfDepend[0];
                if (result.TryGetTarget(out var remoteStream))
                {
                    using (remoteStream)
                    {
                        Player.Tag = downitem;
                        var ct = j.Ct;
                        if (Player.Play(remoteStream, filename, ct) != 0)
                            throw new OperationCanceledException("player cancel");
                    }
                }
                j.Progress = 1;
                j.ProgressStr = "done";
            });

            job.Wait(ct: job.Ct);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////
        /// 
        /// play files with given method(SendUDP, FFplay)
        /// 
        ////////////////////////////////////////////////////////////////////////////////////////////////

        private FormPlayer PlayControler = new FormPlayer();

        private delegate void PlayOneFileDelegate(TSviewCloudPlugin.IRemoteItem downitem, TSviewCloudPlugin.Job master, dynamic data);

        private TSviewCloudPlugin.Job PlayFiles(IEnumerable<TSviewCloudPlugin.IRemoteItem> selectItem, PlayOneFileDelegate func, string LogPrefix, dynamic data = null)
        {
            TSviewCloudConfig.Config.Log.LogOut(LogPrefix + " media files Start.");

            int f_all = selectItem.Count();
            if (f_all == 0) return null;

            var playitems = selectItem.Concat(new TSviewCloudPlugin.IRemoteItem[] { null }).ToArray();


            var job = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.Play);
            job.DisplayName = "Play files";
            job.ProgressStr = "wait for play";
            var ct = job.Ct;
            TSviewCloudPlugin.Job prevjob = job;
            TSviewCloudPlugin.JobControler.Run(job, (j) =>
            {
                j.ProgressStr = "play";
                PlayControler.PlayIndex = 0;

                try
                {
                    while (PlayControler.PlayIndex < f_all)
                    {
                        var playname = playitems[PlayControler.PlayIndex].Name;
                        TSviewCloudConfig.Config.Log.LogOut(LogPrefix + " play : " + playname);

                        PlayControler.CurrentFile = playitems[PlayControler.PlayIndex].Name;
                        PlayControler.NextFile = playitems[PlayControler.PlayIndex + 1]?.Name;

                        try
                        {
                            func(playitems[PlayControler.PlayIndex], j, data);
                        }
                        catch (OperationCanceledException) { }

                        TSviewCloudConfig.Config.Log.LogOut(LogPrefix + " play done : " + playname);

                        PlayControler.PlayIndex++;
                        j.Progress = (double)PlayControler.PlayIndex / f_all;
                    }
                }
                finally
                {
                    PlayControler.Done();
                }

                j.Progress = 1;
            });
            var afterjob = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.Clean, depends: prevjob);
            afterjob.DisplayName = "Clean up";
            return afterjob;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////

        private void button_play_Click(object sender, EventArgs e)
        {
            if (PlayControler.Visible)
            {
                PlayControler.BringToFront();
            }
            else
            {
                PlayControler.Show();
            }

            PlayControler.ClearCallback();
            if (PlayControler.PlayerType == 0)
            {
                PlayControler.StartCallback += (s, evnt) =>
                {
                    if (PlayControler.IsPlaying) return;

                    if (listView1.SelectedIndices.Count > 0)
                    {
                        var selectItems = listView1.SelectedIndices.Cast<int>()
                            .Select(i => listData[i])
                            .Where(item => item.ItemType == TSviewCloudPlugin.RemoteItemType.File)
                            .Where(item => item.Size > 0);

                        if (selectItems.Count() > 0)
                        {
                            PlayWithFFmpeg(selectItems);
                            return;
                        }
                    }
                    PlayControler.Done();
                };
                PlayControler.StopCallback += (s, evnt) =>
                {
                    if (!PlayControler.IsPlaying) return;

                    TSviewCloudPlugin.JobControler.CancelPlay();
                };
                PlayControler.NextCallback += (s, evnt) =>
                {
                    if (!PlayControler.IsPlaying) return;
                    lock (this)
                    {
                        ffplayer?.Stop();
                    }
                };
                PlayControler.PrevCallback += (s, evnt) =>
                {
                    if (!PlayControler.IsPlaying) return;
                    lock (this)
                    {
                        PlayControler.PlayIndex--;
                        ffplayer?.Stop();
                    }
                };
                PlayControler.PositionRequest += (s, evnt) =>
                {
                    PlayControler.StreamDuration = ffplayer?.Duration;
                    PlayControler.StreamPossition = ffplayer?.PlayTime;
                };
                PlayControler.SeekCallback += (s, evnt) =>
                {
                    if (ffplayer != null)
                    {
                        ffplayer.PlayTime = evnt.NewPossition;
                    }
                };

            }
            PlayControler.Start();
        }


        // //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void DownloadFiles(IEnumerable<TSviewCloudPlugin.IRemoteItem> downloaditems)
        {
            if (downloaditems == null || downloaditems.Count() == 0) return;


            if (downloaditems.Count() == 1)
            {
                var item = downloaditems.First();
                if (item.ItemType == TSviewCloudPlugin.RemoteItemType.File)
                {
                    saveFileDialog1.FileName = item.Name;
                    if (saveFileDialog1.ShowDialog() != DialogResult.OK) return;

                    TSviewCloudPlugin.ItemControl.DownloadFile(saveFileDialog1.FileName, item);
                    return;
                }
            }

            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;

            TSviewCloudPlugin.ItemControl.DownloadFolder(folderBrowserDialog1.SelectedPath, downloaditems);
        }

        private void DownloadItems(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                var selectItems = listView1.SelectedIndices.Cast<int>()
                    .Where(i => !listData.IsSpetialItem(i))
                    .Select(i => listData[i]);

                DownloadFiles(selectItems);
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void UploadFolder(TSviewCloudPlugin.IRemoteItem uploadtarget)
        {
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;

            var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                type: TSviewCloudPlugin.JobClass.Display,
                depends: TSviewCloudPlugin.ItemControl.UploadFolder(uploadtarget, folderBrowserDialog1.SelectedPath)
                );
            TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
            {
                if (listData.CurrentViewItem?.FullPath == uploadtarget.FullPath)
                {
                    synchronizationContext.Post((o) =>
                    {
                        GotoAddress(uploadtarget.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                    }, null);
                }
            });
        }

        private void UploadFiles(TSviewCloudPlugin.IRemoteItem uploadtarget)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            var files = openFileDialog1.FileNames;

            var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                type: TSviewCloudPlugin.JobClass.Display,
                depends: TSviewCloudPlugin.ItemControl.UploadFiles(uploadtarget, files).ToArray()
                );
            TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
            {
                if (listData.CurrentViewItem?.FullPath == uploadtarget.FullPath)
                {
                    synchronizationContext.Post((o) =>
                    {
                        GotoAddress(uploadtarget.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                    }, null);
                }
            });
        }

        private void button_upload_Click(object sender, EventArgs e)
        {
            if (listData.CurrentViewItem == null) return;
            if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

            if ((ModifierKeys & Keys.Control) == Keys.Control)
                UploadFolder(listData.CurrentViewItem);
            else
                UploadFiles(listData.CurrentViewItem);
        }


        private void uploadFilesHereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 1)
            {
                var selectItems = listView1.SelectedIndices.Cast<int>()
                    .Select(i => listData[i])
                    .Where(x => x.ItemType == TSviewCloudPlugin.RemoteItemType.Folder);

                if (selectItems.Count() != 1) return;

                UploadFiles(selectItems.First());
            }
            else
            {
                if (listData.CurrentViewItem == null) return;
                if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

                UploadFiles(listData.CurrentViewItem);
            }
        }

        private void uploadFolderHereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 1)
            {
                var selectItems = listView1.SelectedIndices.Cast<int>()
                    .Select(i => listData[i])
                    .Where(x => x.ItemType == TSviewCloudPlugin.RemoteItemType.Folder);

                if (selectItems.Count() != 1) return;

                UploadFolder(selectItems.First());
            }
            else
            {
                if (listData.CurrentViewItem == null) return;
                if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

                UploadFolder(listData.CurrentViewItem);
            }
        }

        private void uploadFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listData.CurrentViewItem == null) return;
            if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

            UploadFiles(listData.CurrentViewItem);
        }

        private void uploadFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listData.CurrentViewItem == null) return;
            if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

            UploadFolder(listData.CurrentViewItem);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void DeleteItems(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                if (MessageBox.Show(
                    string.Format("Are you sure to delete selected {0} items?", listView1.SelectedIndices.Count),
                    "Delete Item(s)",
                    MessageBoxButtons.OKCancel
                    ) != DialogResult.OK)
                {
                    return;
                }

                var selectItems = listView1.SelectedIndices.Cast<int>()
                    .Where(i => !listData.IsSpetialItem(i))
                    .Select(i => listData[i]);

                var currentview = listData.CurrentViewItem;

                var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                    type: TSviewCloudPlugin.JobClass.Display,
                    depends: selectItems.Select(x => x.DeleteItem()).ToArray()
                    );
                TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
                {
                    if (currentview == null) return;
                    if (listData.CurrentViewItem?.FullPath == currentview?.FullPath)
                    {
                        synchronizationContext.Post((o) =>
                        {
                            GotoAddress(currentview.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                        }, null);
                    }
                });
            }
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void MakeFolder(object sender, EventArgs e)
        {
            if (listData.CurrentViewItem == null) return;
            if (listData.CurrentViewItem.ItemType == TSviewCloudPlugin.RemoteItemType.File) return;

            var inputform = new FormInputName();
            if (inputform.ShowDialog(this) == DialogResult.OK)
            {
                var currentview = listData.CurrentViewItem;

                var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                    type: TSviewCloudPlugin.JobClass.Display,
                    depends: listData.CurrentViewItem.MakeFolder(inputform.NewItemName)
                    );
                TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
                {
                    if (currentview == null) return;
                    if (listData.CurrentViewItem?.FullPath == currentview?.FullPath)
                    {
                        synchronizationContext.Post((o) =>
                        {
                            GotoAddress(currentview.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                        }, null);
                    }
                });
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private async void listView1_ItemDragAsync(object sender, ItemDragEventArgs e)
        {
            if (listView1.SelectedIndices.Cast<int>().Any(i => listData.IsSpetialItem(i)))
                return;
            ClipboardRemoteDrive data = null;
            var items = listData.GetItems(listView1.SelectedIndices);
            listView1.Cursor = Cursors.WaitCursor;
            await Task.Run(() =>
            {
                data = new ClipboardRemoteDrive(items);
            });
            listView1.Cursor = Cursors.Default;
            listView1.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
        }

        private IEnumerable<TSviewCloudPlugin.IRemoteItem> GetSelectedItemsFromDataObject(System.Windows.Forms.IDataObject data)
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
            return (ret as string[]).Select(x => TSviewCloudPlugin.RemoteServerFactory.PathToItem(x));
        }

        private void listView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS) || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Point p = listView1.PointToClient(new Point(e.X, e.Y));
                ListViewItem item = listView1.GetItemAt(p.X, p.Y);
                var droptarget = item?.Tag as TSviewCloudPlugin.IRemoteItem;
                var current = listData.CurrentViewItem;

                if (!listData.Contains(droptarget?.FullPath) || droptarget?.ItemType != TSviewCloudPlugin.RemoteItemType.Folder)
                {
                    // display root is target
                }
                else
                {
                    current = droptarget;
                }

                if (current != null)
                {
                    if (current.ItemType == TSviewCloudPlugin.RemoteItemType.Folder)
                    {
                        if (e.Data.GetDataPresent(DataFormats.FileDrop))
                            e.Effect = DragDropEffects.Copy;
                        else
                        {
                            var selectedItems = GetSelectedItemsFromDataObject(e.Data);
                            if ((!selectedItems?.Select(x => x.FullPath).Contains(current.FullPath) ?? false) && (!current.Children?.Select(x => x.FullPath).Intersect(selectedItems.Select(x => x.FullPath)).Any() ?? true))
                            {
                                e.Effect = DragDropEffects.Move;
                            }
                            else
                                e.Effect = DragDropEffects.None;
                        }
                    }
                    else
                        e.Effect = DragDropEffects.None;
                }
                else
                    e.Effect = DragDropEffects.None;
            }
        }


        private void DragDrop_RemoteItem(System.Windows.Forms.IDataObject data, TSviewCloudPlugin.IRemoteItem toParent, string logprefix = "")
        {
            TSviewCloudConfig.Config.Log.LogOut(string.Format("move({0}) Start.", logprefix));

            var selects = GetSelectedItemsFromDataObject(data);
            var prev_current = listData.CurrentViewItem;

            var joblist = new List<TSviewCloudPlugin.Job>();
            foreach (var aItem in selects)
            {
                joblist.Add(aItem.MoveItem(toParent));
            }

            var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                type: TSviewCloudPlugin.JobClass.Display,
                depends: joblist.ToArray()
                );
            TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
            {
                if (listData.CurrentViewItem?.FullPath == toParent.FullPath)
                {
                    synchronizationContext.Post((o) =>
                    {
                        GotoAddress(toParent.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                    }, null);
                }
                else if (prev_current?.FullPath == listData.CurrentViewItem?.FullPath)
                {
                    if (prev_current == null)
                    {

                    }
                    else
                    {
                        synchronizationContext.Post((o) =>
                        {
                            GotoAddress(prev_current.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                        }, null);
                    }
                }
            });
        }


        private void DragDrop_FileDrop(string[] drags, TSviewCloudPlugin.IRemoteItem toParent, string logprefix = "")
        {
            var joblist = new List<TSviewCloudPlugin.Job<TSviewCloudPlugin.IRemoteItem>>();
            var dir_drags = drags.Where(x => Directory.Exists(x)).ToArray();
            var file_drags = drags.Where(x => File.Exists(x)).ToArray();

            joblist.AddRange(TSviewCloudPlugin.ItemControl.UploadFiles(toParent, file_drags));
            joblist.AddRange(dir_drags.Select(d => TSviewCloudPlugin.ItemControl.UploadFolder(toParent, d)));

            var dispjob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                 type: TSviewCloudPlugin.JobClass.Display,
                 depends: joblist.ToArray()
                 );
            TSviewCloudPlugin.JobControler.Run(dispjob, (j) =>
            {
                if (listData.CurrentViewItem?.FullPath == toParent.FullPath)
                {
                    synchronizationContext.Post((o) =>
                    {
                        GotoAddress(toParent.FullPath, TSviewCloudPlugin.ReloadType.Reload);
                    }, null);
                }
            });

        }

        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS) | e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Point p = listView1.PointToClient(new Point(e.X, e.Y));
                ListViewItem item = listView1.GetItemAt(p.X, p.Y);
                var droptarget = item?.Tag as TSviewCloudPlugin.IRemoteItem;
                var current = listData.CurrentViewItem;

                if (!listData.Contains(droptarget?.FullPath) || droptarget?.ItemType != TSviewCloudPlugin.RemoteItemType.Folder)
                {
                    // display root is target
                }
                else
                {
                    current = droptarget;
                }
                if (current == null) return;

                if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
                {
                    DragDrop_RemoteItem(e.Data, current, "listview");
                }
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] drags = (string[])e.Data.GetData(DataFormats.FileDrop);

                    //if (drags.Where(x => Directory.Exists(x)).Count() > 0)
                    //    if (MessageBox.Show(Resource_text.UploadFolder_str, "Folder upload", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

                    DragDrop_FileDrop(drags, current, "listview");
                }
            }

        }



        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        TreeNode HoldonNode;
        private bool supressListviewRefresh;

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            var pos = treeView1.PointToClient(Cursor.Position);
            TreeNode item = treeView1.GetNodeAt(pos.X, pos.Y);

            if (item == null) return;

            if (HoldonNode != item)
            {
                HoldonNode = null;
                return;
            }

            supressListviewRefresh = true;
            try
            {
                var children_kind = item.Nodes.OfType<TreeNode>().Select(x => (x.Tag as TSviewCloudPlugin.IRemoteItem).ItemType);
                if (children_kind.Where(x => x == TSviewCloudPlugin.RemoteItemType.Folder).Count() > 0)
                {
                    item.Expand();
                }
            }
            finally
            {
                supressListviewRefresh = false;
            }
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS) ||
                e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Point p = treeView1.PointToClient(new Point(e.X, e.Y));
                TreeNode item = treeView1.GetNodeAt(p.X, p.Y);
                if (HoldonNode != item)
                    timer1.Enabled = false;
                HoldonNode = item;
                timer1.Enabled = true;

                if (p.Y < treeView1.Height / 2)
                {
                    item?.PrevNode?.EnsureVisible();
                    if (item?.PrevNode == null)
                        item?.Parent?.EnsureVisible();
                }
                else
                {
                    item?.NextNode?.EnsureVisible();
                }

                var targetitem = item?.Tag as TSviewCloudPlugin.IRemoteItem;
                if (targetitem != null)
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        e.Effect = DragDropEffects.Copy;
                    }
                    else
                    {
                        e.Effect = DragDropEffects.Move;

                        if (item != null)
                        {
                            while (targetitem.ItemType != TSviewCloudPlugin.RemoteItemType.Folder)
                            {
                                item = item.Parent;
                                if (item == null) break;
                                targetitem = item.Tag as TSviewCloudPlugin.IRemoteItem;
                            }
                        }
                        var toParent = (item?.Tag as TSviewCloudPlugin.IRemoteItem);
                        foreach (var aItem in GetSelectedItemsFromDataObject(e.Data))
                        {
                            var fromParent = aItem.Parents.FirstOrDefault();
                            if (toParent == fromParent || toParent == aItem)
                            {
                                e.Effect = DragDropEffects.None;
                                break;
                            }
                        }
                    }
                }
                else
                    e.Effect = DragDropEffects.None;
            }

        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS) ||
                    e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Point p = treeView1.PointToClient(new Point(e.X, e.Y));
                TreeNode item = treeView1.GetNodeAt(p.X, p.Y);

                if (item != null)
                {
                    while ((item.Tag as TSviewCloudPlugin.IRemoteItem)?.ItemType != TSviewCloudPlugin.RemoteItemType.Folder)
                    {
                        item = item.Parent;
                        if (item == null) break;
                    }
                }

                var toParent = item?.Tag as TSviewCloudPlugin.IRemoteItem;
                if (toParent == null)
                    return;

                if (e.Data.GetDataPresent(ClipboardRemoteDrive.CFSTR_CLOUD_DRIVE_ITEMS))
                {
                    DragDrop_RemoteItem(e.Data, toParent, "treeview");
                }
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] drags = (string[])e.Data.GetData(DataFormats.FileDrop);

                    //if (drags.Where(x => Directory.Exists(x)).Count() > 0)
                    //    if (MessageBox.Show(Resource_text.UploadFolder_str, "Folder upload", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

                    DragDrop_FileDrop(drags, toParent, "treeview");
                }
            }

        }
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void driveCacheclearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TSviewCloudPlugin.RemoteServerFactory.ClearCache();

            foreach (TreeNode node in treeView1.Nodes)
            {
                node.Collapse();
                node.Nodes.Clear();
            }
            listData.Clear();

            var load2Job = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.ControlMaster);
            TSviewCloudPlugin.JobControler.Run(load2Job, (j) =>
            {
                var display2Job = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(
                    type: TSviewCloudPlugin.JobClass.LoadItem,
                    depends: TSviewCloudPlugin.RemoteServerFactory.ServerList.Values
                        .Select(s => TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(s.Name + "://")).ToArray()
                );
                display2Job.DisplayName = "Display  server";
                display2Job.ProgressStr = "wait for load";
                TSviewCloudPlugin.JobControler.Run<TSviewCloudPlugin.IRemoteItem>(display2Job, (j2j) =>
                {
                    j2j.Progress = -1;
                    j2j.ProgressStr = "Loading...";
                    var results = new List<TSviewCloudPlugin.IRemoteItem>();
                    foreach (var item in j2j.ResultOfDepend)
                    {
                        if (item.TryGetTarget(out var result))
                        {
                            results.Add(result);
                        }
                    }

                    synchronizationContext.Send((o) =>
                    {
                        foreach (var item in o as IEnumerable<TSviewCloudPlugin.IRemoteItem>)
                        {
                            var treenode = treeView1.Nodes.Find(item.Server, false).First();
                            treenode.Tag = item;
                            ExpandItem(treenode);
                        }
                    }, results);
                });
            });
        }


        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void SearchItems(object sender, EventArgs e)
        {
            var search = new FormSearch();
            search.SearchResultCallback += (s, evnt) =>
            {
                listData.SetSearchResults(search.SearchResult);
            };
            search.SearchSelectCallback += (s, evnt) =>
            {
                if (listView1.SelectedIndices.Count > 0)
                    search.SelectedItems = listData.GetItems(listView1.SelectedIndices).ToArray();
            };
            search.Show();
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private void DiffItems(object sender, EventArgs e)
        {


        }
    }



    static class Extensions
    {
        public static IOrderedEnumerable<TSviewCloudPlugin.IRemoteItem> SortByKind(this IEnumerable<TSviewCloudPlugin.IRemoteItem> x, bool SortKind)
        {
            return x.OrderBy(y => (SortKind) ? (y.ItemType == TSviewCloudPlugin.RemoteItemType.File) : true);
        }
    }

}
