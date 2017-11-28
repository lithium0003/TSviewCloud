using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloud
{
    public partial class FormDiffResult : Form
    {
        public FormDiffResult()
        {
            InitializeComponent();
        }

        public IEnumerable<FormDiff.MatchItem> LocalOnly
        {
            set
            {
                listBox_LocalOnly.DataSource = value;
            }
        }
        public IEnumerable<FormDiff.MatchItem> RemoteOnly
        {
            set
            {
                listBox_RemoteOnly.DataSource = value;
            }
        }
        public IEnumerable<FormDiff.MatchItem> Unmatch
        {
            set
            {
                foreach (var item in value)
                {
                    var newitem = new ListViewItem(new string[]{
                        item.remoteA.info.Hash,
                        item.remoteA.info.Size.ToString(),
                        item.remoteA.path,
                        item.remoteB.info.Hash,
                        item.remoteB.info.Size.ToString(),
                        item.remoteB.path,
                    });
                    newitem.Tag = item;
                    listView_Unmatch.Items.Add(newitem);
                }
            }
        }
        public IEnumerable<FormDiff.MatchItem> Match
        {
            set
            {
                foreach (var item in value)
                {
                    var newitem = new ListViewItem(new string[]{
                        item.remoteA.path,
                        item.remoteB.path,
                        item.remoteA.info.Size.ToString(),
                        item.remoteA.info.Hash,
                    });
                    newitem.Tag = item;
                    listView_Match.Items.Add(newitem);
                }
            }
        }
        public IDictionary<string, FormDiff.RemoteItemInfo[]> RemoteADup
        {
            set
            {
                foreach (var item in value)
                {
                    var node = treeView_localDup.Nodes.Add(item.Key);
                    foreach (var ditem in item.Value)
                    {
                        TreeNode newitem;
                        if (ditem.info.Hash == null)
                            newitem = new TreeNode(string.Format("size:{0} {1}", ditem.info.Size, ditem.path));
                        else
                            newitem = new TreeNode(string.Format("size:{0} Hash:{1} {2}", ditem.info.Size, ditem.info.Hash, ditem.path));
                        newitem.Tag = ditem;
                        node.Nodes.Add(newitem);
                    }
                }
            }
        }
        public IDictionary<string, FormDiff.RemoteItemInfo[]> RemoteBDup
        {
            set
            {
                foreach (var item in value)
                {
                    var node = treeView_remoteDup.Nodes.Add(item.Key);
                    foreach (var ditem in item.Value)
                    {
                        TreeNode newitem;
                        if (ditem.info.Hash == null)
                            newitem = new TreeNode(string.Format("size:{0} {1}", ditem.info.Size, ditem.path));
                        else
                            newitem = new TreeNode(string.Format("size:{0} Hash:{1} {2}", ditem.info.Size, ditem.info.Hash, ditem.path));
                        newitem.Tag = ditem;
                        node.Nodes.Add(newitem);
                    }
                }
            }
        }

        private void listBox_LocalOnly_Format(object sender, ListControlConvertEventArgs e)
        {
            var item = e.ListItem as FormDiff.MatchItem;
            e.Value = item.remoteA.path;
        }

        private void listBox_RemoteOnly_Format(object sender, ListControlConvertEventArgs e)
        {
            var item = e.ListItem as FormDiff.MatchItem;
            e.Value = item.remoteB.path;
        }

        delegate void SaveDataFunc(StreamWriter sw);

        private void SaveList(SaveDataFunc Func)
        {
            if (Func == null) return;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                var filename = saveFileDialog1.FileName;
                using (var fs = new FileStream(filename, FileMode.Create))
                using (var sw = new StreamWriter(fs))
                {
                    Func(sw);
                }
            }
        }

        private void button_SaveLocalList_Click(object sender, EventArgs e)
        {
            SaveList(sw =>
            {
                if (listBox_LocalOnly.DataSource != null)
                {
                    foreach (var item in listBox_LocalOnly.DataSource as IEnumerable<FormDiff.MatchItem>)
                    {
                        sw.WriteLine("{0},{1},{2},{3}",
                            item.remoteA.path,
                            item.remoteA.info.FullPath,
                            item.remoteA.info.Size,
                            item.remoteA.info.Hash);
                    }
                }
            });
        }

        private void button_SaveRemoteList_Click(object sender, EventArgs e)
        {
            SaveList(sw =>
            {
                if (listBox_RemoteOnly.DataSource != null)
                {
                    sw.WriteLine("Path,id,size,MD5");
                    foreach (var item in listBox_RemoteOnly.DataSource as IEnumerable<FormDiff.MatchItem>)
                    {
                        sw.WriteLine("{0},{1},{2},{3}",
                            item.remoteB.path,
                            item.remoteB.info.FullPath,
                            item.remoteB.info.Size,
                            item.remoteB.info.Hash);
                    }
                }
            });
        }

        private void button_SaveUnmatchList_Click(object sender, EventArgs e)
        {
            if (listView_Unmatch.Items.Count == 0)
                return;
            SaveList(sw =>
            {
                sw.WriteLine("LocalPath,LocalSize,LocalMD5,RemotePath,RemoteSize,RemoteMD5,RemoteID");
                foreach (ListViewItem item in listView_Unmatch.Items)
                {
                    if (item.Tag != null)
                    {
                        var data = item.Tag as FormDiff.MatchItem;
                        sw.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                            data.remoteA.path,
                            data.remoteA.info.Size,
                            data.remoteA.info.Hash,
                            data.remoteA.info.FullPath,
                            data.remoteB.path,
                            data.remoteB.info.Size,
                            data.remoteB.info.Hash,
                            data.remoteB.info.FullPath);
        }
    }
            });
        }

        private void button_SaveLocalDupList_Click(object sender, EventArgs e)
        {
            if (treeView_localDup.Nodes.Count == 0)
                return;
            SaveList(sw =>
            {
                foreach (TreeNode node1 in treeView_localDup.Nodes)
                {
                    sw.WriteLine(node1.Text);
                    foreach (TreeNode node2 in node1.Nodes)
                    {
                        var item = node2.Tag as FormDiff.RemoteItemInfo;
                        sw.WriteLine("\t{0},{1},{2},{3}",
                            item.path,
                            item.info.Size,
                            item.info.Hash,
                            item.info.FullPath);
                    }
                }
            });
        }

        private void button_SaveRemoteDupList_Click(object sender, EventArgs e)
        {
            if (treeView_remoteDup.Nodes.Count == 0)
                return;
            SaveList(sw =>
            {
                foreach (TreeNode node1 in treeView_remoteDup.Nodes)
                {
                    sw.WriteLine(node1.Text);
                    foreach (TreeNode node2 in node1.Nodes)
                    {
                        var item = node2.Tag as FormDiff.RemoteItemInfo;
                        sw.WriteLine("\t{0},{1},{2},{3}",
                            item.path,
                            item.info.Size,
                            item.info.Hash,
                            item.info.FullPath);
                    }
                }
            });
        }

        private void button_SaveMatchedList_Click(object sender, EventArgs e)
        {
            if (listView_Match.Items.Count == 0)
                return;
            SaveList(sw =>
            {
                sw.WriteLine("LocalPath,RemotePath,Size,MD5,RemoteID");
                foreach (ListViewItem item in listView_Match.Items)
                {
                    if (item.Tag != null)
                    {
                        var data = item.Tag as FormDiff.MatchItem;
                        sw.WriteLine("{0},{1},{2},{3},{4}",
                            data.remoteA.path,
                            data.remoteB.path,
                            data.remoteA.info.Size,
                            data.remoteA.info.Hash,
                            data.remoteA.info.FullPath);
                    }
                }
            });
        }

        private void button_Download_Click(object sender, EventArgs e)
        {
            button_Download.Enabled = false;
            try
            {
                var items = listBox_RemoteOnly.SelectedItems;
                if (items.Count == 0) return;

            }
            finally
            {
                button_Download.Enabled = true;
            }
        }

        private void button_trash_Click(object sender, EventArgs e)
        {
            button_trash.Enabled = false;
            try
            {
                var items = listBox_RemoteOnly.SelectedItems;
                if (items.Count == 0) return;

            }
            finally
            {
                button_trash.Enabled = true;
            }
        }
    }
}
