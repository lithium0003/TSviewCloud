using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSviewCloudPlugin;

namespace TSviewCloud
{
    public partial class FormTreeSelect : Form
    {
        SynchronizationContext synchronizationContext;

        IRemoteItem _selectedItem;

        public IRemoteItem SelectedItem { get => _selectedItem;  }

        public FormTreeSelect()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
        }

        private void FormTreeSelect_Load(object sender, EventArgs e)
        {
            var smallimagelist = new ImageList();
            smallimagelist.Images.Add(Properties.Resources.File);
            smallimagelist.Images.Add(Properties.Resources.Folder);
            treeView1.ImageList = smallimagelist;

            foreach (var server in TSviewCloudPlugin.RemoteServerFactory.ServerList.Values)
            {
                if (!server.IsReady) continue;

                treeView1.ImageList.Images.Add(server.Icon);

                var root = new TreeNode(server.Name, treeView1.ImageList.Images.Count - 1, treeView1.ImageList.Images.Count - 1)
                {
                    Name = server.Name,
                    Tag = server.PeakItem("")
                };
                treeView1.Nodes.Add(root);
                ExpandItem(root);
            }
        }

        private TSviewCloudPlugin.Job ExpandItem(TreeNode baseNode)
        {
            var pitem = baseNode?.Tag as TSviewCloudPlugin.IRemoteItem;
            if (pitem != null)
            {
                var loadjob = TSviewCloudPlugin.RemoteServerFactory.PathToItemJob(pitem.FullPath, TSviewCloudPlugin.ReloadType.Cache);

                var DisplayJob = TSviewCloudPlugin.JobControler.CreateNewJob<TSviewCloudPlugin.IRemoteItem>(TSviewCloudPlugin.JobClass.LoadItem, depends: loadjob);
                DisplayJob.DisplayName = "Display  " + pitem.FullPath;
                TSviewCloudPlugin.JobControler.Run(DisplayJob, (j) =>
                {
                    DisplayJob.Progress = -1;
                    DisplayJob.ProgressStr = "Loading...";
                    DisplayJob.ForceHidden = true;

                    var children = DisplayJob.ResultOfDepend[0].Children;

                    synchronizationContext.Send((o) =>
                    {
                        try
                        {
                            baseNode.Nodes.AddRange(
                                GenerateTreeNode(o as IEnumerable<TSviewCloudPlugin.IRemoteItem>)
                                .OrderByDescending(x => (x.Tag as TSviewCloudPlugin.IRemoteItem).ItemType)
                                .ThenBy(x => (x.Tag as TSviewCloudPlugin.IRemoteItem).Name)
                                .ToArray()
                            );
                        }
                        finally
                        {

                            DisplayJob.ProgressStr = "done.";
                            DisplayJob.Progress = 1;
                        }
                    }, children);
                });
                return DisplayJob;
            }
            return null;
        }

        private IEnumerable<TreeNode> GenerateTreeNode(IEnumerable<TSviewCloudPlugin.IRemoteItem> children, int count = 0)
        {
            var ret = new List<TreeNode>();
            if (children == null) return ret;
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
                        GenerateTreeNode(x.Children, count - 1)
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
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null) return;

            var joblist = new List<TSviewCloudPlugin.Job>();
            foreach (TreeNode item in e.Node.Nodes)
            {
                if (item.Nodes.Count > 0) continue;
                joblist.Add(ExpandItem(item));
            }
            if (joblist.Count == 0) return;

            var finishjob = TSviewCloudPlugin.JobControler.CreateNewJob(TSviewCloudPlugin.JobClass.Clean, depends: joblist.ToArray());
            TSviewCloudPlugin.JobControler.Run(finishjob, (j) =>
            {
            });
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null) return;

            _selectedItem = e.Node.Tag as IRemoteItem;
        }
    }
}
