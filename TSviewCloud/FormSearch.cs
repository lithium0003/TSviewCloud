using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSviewCloudPlugin;

namespace TSviewCloud
{
    public partial class FormSearch : Form
    {
        public FormSearch()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
        }

        private void button_SelectTree_Click(object sender, EventArgs e)
        {
            var picker = new FormTreeSelect
            {
                Text = "Select encrypt root folder"
            };

            using (picker)
            {
                if (picker.ShowDialog() != DialogResult.OK) return;
                if (picker.SelectedItem == null) return;

                textBox_SearchFolder.Text = picker.SelectedItem.FullPath;
            }
        }

        private void checkBox_regex_CheckedChanged(object sender, EventArgs e)
        {
            radioButton_contain.Enabled = radioButton_startswith.Enabled = radioButton_endswith.Enabled = (!checkBox_regex.Checked && !checkBox_case.Checked);
        }

        private void checkBox_case_CheckedChanged(object sender, EventArgs e)
        {
            radioButton_contain.Enabled = radioButton_startswith.Enabled = radioButton_endswith.Enabled = (!checkBox_regex.Checked && !checkBox_case.Checked);
        }

        private void radioButton_SerachFolder_CheckedChanged(object sender, EventArgs e)
        {
            textBox_SearchFolder.Enabled = button_SelectTree.Enabled = radioButton_SerachFolder.Checked;
        }

        private void button_seach_Click(object sender, EventArgs e)
        {
            if (comboBox_name.Items.IndexOf(comboBox_name.Text) < 0)
                comboBox_name.Items.Add(comboBox_name.Text);

            SearchSelectCallback?.Invoke(this, new EventArgs());
            DoSearch();
        }

        private IEnumerable<IRemoteItem> GetItems(IRemoteItem rootitem)
        {
            SearchJob?.Ct.ThrowIfCancellationRequested();

            List<IRemoteItem> ret = new List<IRemoteItem>();

            if (rootitem == null) return ret;
            var target = rootitem.Children;
            if (target == null) return ret;

            Parallel.ForEach(
                target,
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

        private void DoSearch()
        {
            if (radioButton_selected.Checked && SelectedItems == null) return;

            button_seach.Enabled = false;
            button_cancel.Enabled = true;
            button_showresult.Enabled = false;

            var typeAll = radioButton_typeAll.Checked;
            var typeFolder = radioButton_typeFolder.Checked;
            var typeFile = radioButton_typeFile.Checked;

            var selectall = radioButton_SearchAll.Checked;
            var selectitem = radioButton_selected.Checked;
            var selecttree = radioButton_SerachFolder.Checked;
            var treepath = textBox_SearchFolder.Text;

            var searchStr = comboBox_name.Text;

            var strstarts = radioButton_startswith.Checked;
            var strends = radioButton_endswith.Checked;
            var strcontain = radioButton_contain.Checked;

            var strregex = checkBox_regex.Checked;
            var strcase = checkBox_case.Checked;

            var SizeOver = checkBox_Over.Checked;
            var SizeUnder = checkBox_Under.Checked;
            var Over = numericUpDown_over.Value;
            var Under = numericUpDown_under.Value;

            var modifiedFromEnable = dateTimePicker_modifiedFrom.Checked;
            var modifiedToEnable = dateTimePicker_modifiedTo.Checked;
            var createdFromEnable = dateTimePicker_createdFrom.Checked;
            var createdToEnable = dateTimePicker_createdTo.Checked;
            var modifiedFrom = dateTimePicker_modifiedFrom.Value;
            var modifiedTo = dateTimePicker_modifiedTo.Value;
            var createdFrom = dateTimePicker_createdFrom.Value;
            var createdTo = dateTimePicker_createdTo.Value;

            progressBar1.Style = ProgressBarStyle.Marquee;
            label_result.Text = "wait for system...";

            SearchJob?.Cancel();
            SearchJob = JobControler.CreateNewJob();
            SearchJob.DisplayName = "Search";
            JobControler.Run(SearchJob, (j) =>
            {
                j.ProgressStr = "Create index...";
                j.Progress = -1;

                TSviewCloudConfig.Config.Log.LogOut("[Search] start");
                var sw = new System.Diagnostics.Stopwatch();
                try
                {
                    List<IRemoteItem> initselection = new List<IRemoteItem>();
                    List<IRemoteItem> selection = new List<IRemoteItem>();

                    if (selectall)
                    {
                        initselection.AddRange(RemoteServerFactory.ServerList.Values.Select(x => x[""]));
                    }
                    if (selectitem)
                    {
                        initselection.AddRange(SelectedItems);
                    }
                    if (selecttree)
                    {
                        initselection.Add(RemoteServerFactory.PathToItem(treepath));
                    }

                    synchronizationContext.Post((o) =>
                    {
                        label_result.Text = o as string;
                    }, "Prepare items...");

                    TSviewCloudConfig.Config.Log.LogOut("[Search] Create index");
                    sw.Start();

                    Parallel.ForEach(
                        initselection,
                        () => new List<IRemoteItem>(),
                        (x, state, local) =>
                        {
                            if (x == null) return local;
                            var item = RemoteServerFactory.PathToItem(x.FullPath);
                            if (item == null) return local;

                            local.AddRange(GetItems(item));
                            return local;
                        },
                        (result) =>
                        {
                            lock (selection)
                                selection.AddRange(result);
                        }
                    );

                    synchronizationContext.Post((o) =>
                    {
                        label_result.Text = o as string;
                    }, "Prepare items done.");
                    sw.Stop();
                    var itemsearch_time = sw.Elapsed;
                    


                    var search = selection.AsParallel();

                    if (typeFile)
                        search = search.Where(x => x.ItemType == RemoteItemType.File);
                    if (typeFolder)
                        search = search.Where(x => x.ItemType == RemoteItemType.Folder);


                    if (strregex)
                    {
                        if (!strcase)
                            search = search.Where(x => Regex.IsMatch(x.Name ?? "", searchStr));
                        else
                            search = search.Where(x => Regex.IsMatch(x.Name ?? "", searchStr, RegexOptions.IgnoreCase));
                    }
                    else
                    {
                        if (!strcase)
                        {
                            if (strstarts)
                                search = search.Where(x => (x.Name?.StartsWith(searchStr) ?? searchStr == ""));
                            if (strends)
                                search = search.Where(x => (x.Name?.EndsWith(searchStr) ?? searchStr == ""));
                            if (strcontain)
                                search = search.Where(x => (x.Name?.IndexOf(searchStr) >= 0));
                        }
                        else
                            search = search.Where(x => (
                            System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                                x.Name ?? "",
                                searchStr,
                                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreKanaType | System.Globalization.CompareOptions.IgnoreWidth
                                | System.Globalization.CompareOptions.IgnoreNonSpace | System.Globalization.CompareOptions.IgnoreSymbols
                                ) >= 0));
                    }

                    if (SizeOver)
                        search = search.Where(x => (x.Size ?? 0) > Over);
                    if (SizeUnder)
                        search = search.Where(x => (x.Size ?? 0) < Under);


                    if (modifiedFromEnable)
                        search = search.Where(x => x.ModifiedDate > modifiedFrom);
                    if (modifiedToEnable)
                        search = search.Where(x => x.ModifiedDate < modifiedTo);

                    if (createdFromEnable)
                        search = search.Where(x => x.CreatedDate > createdFrom);
                    if (createdToEnable)
                        search = search.Where(x => x.CreatedDate < createdTo);

                    synchronizationContext.Post((o) =>
                    {
                        label_result.Text = o as string;
                    }, "Search...");
                    j.ProgressStr = "Search...";

                    TSviewCloudConfig.Config.Log.LogOut("[Search] Search");
                    sw.Restart();

                    SearchResult = search.ToArray();

                    sw.Stop();
                    var filteritem_time = sw.Elapsed;

                    j.Progress = 1;
                    j.ProgressStr = "Found : " + SearchResult.Count().ToString();

                    synchronizationContext.Post((o) =>
                    {
                        label_result.Text = o as string;
                        button_seach.Enabled = true;
                        button_cancel.Enabled = false;
                        button_showresult.Enabled = true;
                        progressBar1.Style = ProgressBarStyle.Continuous;
                        SearchJob = null;
                    }, string.Format("Found : {0}, Index {1} search {2}", SearchResult.Count(), itemsearch_time, filteritem_time));

                    TSviewCloudConfig.Config.Log.LogOut("[Search] found "+ SearchResult.Count().ToString());
                }
                catch
                {
                    TSviewCloudConfig.Config.Log.LogOut("[Search] Abort");

                    synchronizationContext.Post((o) =>
                    {
                        label_result.Text = "abort";
                        button_seach.Enabled = true;
                        button_cancel.Enabled = false;
                        progressBar1.Style = ProgressBarStyle.Continuous;
                        SearchJob = null;
                    }, null);
                }
            });
        }

        IEnumerable<IRemoteItem> _SelectedItems;
        IEnumerable<IRemoteItem> _SearchResult;
        Job SearchJob;
        EventHandler _searchResultCallback;
        EventHandler _searchSelectCallback;
        private SynchronizationContext synchronizationContext;

        public EventHandler SearchResultCallback { get => _searchResultCallback; set => _searchResultCallback = value; }
        public EventHandler SearchSelectCallback { get => _searchSelectCallback; set => _searchSelectCallback = value; }
        public IEnumerable<IRemoteItem> SelectedItems { get => _SelectedItems; set => _SelectedItems = value; }
        public IEnumerable<IRemoteItem> SearchResult { get => _SearchResult; set => _SearchResult = value; }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            SearchJob?.Cancel();
            button_seach.Enabled = true;
            button_cancel.Enabled = false;
            button_showresult.Enabled = false;
        }

        private void button_showresult_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            SearchResultCallback?.Invoke(this, new EventArgs());
        }

    }
}
