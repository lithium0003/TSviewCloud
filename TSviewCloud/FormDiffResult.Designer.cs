namespace TSviewCloud
{
    partial class FormDiffResult
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormDiffResult));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.listBox_LocalOnly = new System.Windows.Forms.ListBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_trashA = new System.Windows.Forms.Button();
            this.button_DownloadA = new System.Windows.Forms.Button();
            this.button_SaveLocalList = new System.Windows.Forms.Button();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.listBox_RemoteOnly = new System.Windows.Forms.ListBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.button_trashB = new System.Windows.Forms.Button();
            this.button_DownloadB = new System.Windows.Forms.Button();
            this.button_SaveRemoteList = new System.Windows.Forms.Button();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.listView_Unmatch = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader9 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader11 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel5 = new System.Windows.Forms.Panel();
            this.button_SaveUnmatchList = new System.Windows.Forms.Button();
            this.splitter3 = new System.Windows.Forms.Splitter();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.listView_Match = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel4 = new System.Windows.Forms.Panel();
            this.button_SaveMatchedList = new System.Windows.Forms.Button();
            this.splitter4 = new System.Windows.Forms.Splitter();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.treeView_localDup = new System.Windows.Forms.TreeView();
            this.panel7 = new System.Windows.Forms.Panel();
            this.button_SaveLocalDupList = new System.Windows.Forms.Button();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.treeView_remoteDup = new System.Windows.Forms.TreeView();
            this.panel6 = new System.Windows.Forms.Panel();
            this.button_SaveRemoteDupList = new System.Windows.Forms.Button();
            this.splitter5 = new System.Windows.Forms.Splitter();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.groupBox1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.panel2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.panel5.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.panel4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.panel7.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.panel6.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.listBox_LocalOnly);
            this.groupBox1.Controls.Add(this.panel1);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // listBox_LocalOnly
            // 
            resources.ApplyResources(this.listBox_LocalOnly, "listBox_LocalOnly");
            this.listBox_LocalOnly.FormattingEnabled = true;
            this.listBox_LocalOnly.Name = "listBox_LocalOnly";
            this.listBox_LocalOnly.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_LocalOnly.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.listBox_LocalOnly_Format);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.button_trashA);
            this.panel1.Controls.Add(this.button_DownloadA);
            this.panel1.Controls.Add(this.button_SaveLocalList);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // button_trashA
            // 
            resources.ApplyResources(this.button_trashA, "button_trashA");
            this.button_trashA.Name = "button_trashA";
            this.button_trashA.UseVisualStyleBackColor = true;
            this.button_trashA.Click += new System.EventHandler(this.button_trashA_Click);
            // 
            // button_DownloadA
            // 
            resources.ApplyResources(this.button_DownloadA, "button_DownloadA");
            this.button_DownloadA.Name = "button_DownloadA";
            this.button_DownloadA.UseVisualStyleBackColor = true;
            this.button_DownloadA.Click += new System.EventHandler(this.button_DownloadA_Click);
            // 
            // button_SaveLocalList
            // 
            resources.ApplyResources(this.button_SaveLocalList, "button_SaveLocalList");
            this.button_SaveLocalList.Name = "button_SaveLocalList";
            this.button_SaveLocalList.UseVisualStyleBackColor = true;
            this.button_SaveLocalList.Click += new System.EventHandler(this.button_SaveLocalList_Click);
            // 
            // splitter1
            // 
            resources.ApplyResources(this.splitter1, "splitter1");
            this.splitter1.Name = "splitter1";
            this.splitter1.TabStop = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.listBox_RemoteOnly);
            this.groupBox2.Controls.Add(this.panel2);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // listBox_RemoteOnly
            // 
            resources.ApplyResources(this.listBox_RemoteOnly, "listBox_RemoteOnly");
            this.listBox_RemoteOnly.FormattingEnabled = true;
            this.listBox_RemoteOnly.Name = "listBox_RemoteOnly";
            this.listBox_RemoteOnly.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_RemoteOnly.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.listBox_RemoteOnly_Format);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.button_trashB);
            this.panel2.Controls.Add(this.button_DownloadB);
            this.panel2.Controls.Add(this.button_SaveRemoteList);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // button_trashB
            // 
            resources.ApplyResources(this.button_trashB, "button_trashB");
            this.button_trashB.Name = "button_trashB";
            this.button_trashB.UseVisualStyleBackColor = true;
            this.button_trashB.Click += new System.EventHandler(this.button_trashB_Click);
            // 
            // button_DownloadB
            // 
            resources.ApplyResources(this.button_DownloadB, "button_DownloadB");
            this.button_DownloadB.Name = "button_DownloadB";
            this.button_DownloadB.UseVisualStyleBackColor = true;
            this.button_DownloadB.Click += new System.EventHandler(this.button_DownloadB_Click);
            // 
            // button_SaveRemoteList
            // 
            resources.ApplyResources(this.button_SaveRemoteList, "button_SaveRemoteList");
            this.button_SaveRemoteList.Name = "button_SaveRemoteList";
            this.button_SaveRemoteList.UseVisualStyleBackColor = true;
            this.button_SaveRemoteList.Click += new System.EventHandler(this.button_SaveRemoteList_Click);
            // 
            // splitter2
            // 
            resources.ApplyResources(this.splitter2, "splitter2");
            this.splitter2.Name = "splitter2";
            this.splitter2.TabStop = false;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.listView_Unmatch);
            this.groupBox3.Controls.Add(this.panel5);
            resources.ApplyResources(this.groupBox3, "groupBox3");
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.TabStop = false;
            // 
            // listView_Unmatch
            // 
            this.listView_Unmatch.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader7,
            this.columnHeader8,
            this.columnHeader9,
            this.columnHeader10,
            this.columnHeader11});
            resources.ApplyResources(this.listView_Unmatch, "listView_Unmatch");
            this.listView_Unmatch.Name = "listView_Unmatch";
            this.listView_Unmatch.UseCompatibleStateImageBehavior = false;
            this.listView_Unmatch.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            resources.ApplyResources(this.columnHeader1, "columnHeader1");
            // 
            // columnHeader7
            // 
            resources.ApplyResources(this.columnHeader7, "columnHeader7");
            // 
            // columnHeader8
            // 
            resources.ApplyResources(this.columnHeader8, "columnHeader8");
            // 
            // columnHeader9
            // 
            resources.ApplyResources(this.columnHeader9, "columnHeader9");
            // 
            // columnHeader10
            // 
            resources.ApplyResources(this.columnHeader10, "columnHeader10");
            // 
            // columnHeader11
            // 
            resources.ApplyResources(this.columnHeader11, "columnHeader11");
            // 
            // panel5
            // 
            this.panel5.Controls.Add(this.button_SaveUnmatchList);
            resources.ApplyResources(this.panel5, "panel5");
            this.panel5.Name = "panel5";
            // 
            // button_SaveUnmatchList
            // 
            resources.ApplyResources(this.button_SaveUnmatchList, "button_SaveUnmatchList");
            this.button_SaveUnmatchList.Name = "button_SaveUnmatchList";
            this.button_SaveUnmatchList.UseVisualStyleBackColor = true;
            this.button_SaveUnmatchList.Click += new System.EventHandler(this.button_SaveUnmatchList_Click);
            // 
            // splitter3
            // 
            resources.ApplyResources(this.splitter3, "splitter3");
            this.splitter3.Name = "splitter3";
            this.splitter3.TabStop = false;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.listView_Match);
            this.groupBox4.Controls.Add(this.panel4);
            resources.ApplyResources(this.groupBox4, "groupBox4");
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.TabStop = false;
            // 
            // listView_Match
            // 
            this.listView_Match.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4,
            this.columnHeader5,
            this.columnHeader6});
            resources.ApplyResources(this.listView_Match, "listView_Match");
            this.listView_Match.FullRowSelect = true;
            this.listView_Match.Name = "listView_Match";
            this.listView_Match.UseCompatibleStateImageBehavior = false;
            this.listView_Match.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            resources.ApplyResources(this.columnHeader3, "columnHeader3");
            // 
            // columnHeader4
            // 
            resources.ApplyResources(this.columnHeader4, "columnHeader4");
            // 
            // columnHeader5
            // 
            resources.ApplyResources(this.columnHeader5, "columnHeader5");
            // 
            // columnHeader6
            // 
            resources.ApplyResources(this.columnHeader6, "columnHeader6");
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.button_SaveMatchedList);
            resources.ApplyResources(this.panel4, "panel4");
            this.panel4.Name = "panel4";
            // 
            // button_SaveMatchedList
            // 
            resources.ApplyResources(this.button_SaveMatchedList, "button_SaveMatchedList");
            this.button_SaveMatchedList.Name = "button_SaveMatchedList";
            this.button_SaveMatchedList.UseVisualStyleBackColor = true;
            this.button_SaveMatchedList.Click += new System.EventHandler(this.button_SaveMatchedList_Click);
            // 
            // splitter4
            // 
            resources.ApplyResources(this.splitter4, "splitter4");
            this.splitter4.Name = "splitter4";
            this.splitter4.TabStop = false;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.treeView_localDup);
            this.groupBox5.Controls.Add(this.panel7);
            resources.ApplyResources(this.groupBox5, "groupBox5");
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.TabStop = false;
            // 
            // treeView_localDup
            // 
            resources.ApplyResources(this.treeView_localDup, "treeView_localDup");
            this.treeView_localDup.Name = "treeView_localDup";
            // 
            // panel7
            // 
            this.panel7.Controls.Add(this.button_SaveLocalDupList);
            resources.ApplyResources(this.panel7, "panel7");
            this.panel7.Name = "panel7";
            // 
            // button_SaveLocalDupList
            // 
            resources.ApplyResources(this.button_SaveLocalDupList, "button_SaveLocalDupList");
            this.button_SaveLocalDupList.Name = "button_SaveLocalDupList";
            this.button_SaveLocalDupList.UseVisualStyleBackColor = true;
            this.button_SaveLocalDupList.Click += new System.EventHandler(this.button_SaveLocalDupList_Click);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.treeView_remoteDup);
            this.groupBox6.Controls.Add(this.panel6);
            resources.ApplyResources(this.groupBox6, "groupBox6");
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.TabStop = false;
            // 
            // treeView_remoteDup
            // 
            resources.ApplyResources(this.treeView_remoteDup, "treeView_remoteDup");
            this.treeView_remoteDup.Name = "treeView_remoteDup";
            // 
            // panel6
            // 
            this.panel6.Controls.Add(this.button_SaveRemoteDupList);
            resources.ApplyResources(this.panel6, "panel6");
            this.panel6.Name = "panel6";
            // 
            // button_SaveRemoteDupList
            // 
            resources.ApplyResources(this.button_SaveRemoteDupList, "button_SaveRemoteDupList");
            this.button_SaveRemoteDupList.Name = "button_SaveRemoteDupList";
            this.button_SaveRemoteDupList.UseVisualStyleBackColor = true;
            this.button_SaveRemoteDupList.Click += new System.EventHandler(this.button_SaveRemoteDupList_Click);
            // 
            // splitter5
            // 
            resources.ApplyResources(this.splitter5, "splitter5");
            this.splitter5.Name = "splitter5";
            this.splitter5.TabStop = false;
            // 
            // FormDiffResult
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitter5);
            this.Controls.Add(this.groupBox6);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.splitter4);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.splitter3);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.splitter2);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.groupBox1);
            this.Name = "FormDiffResult";
            this.groupBox1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.panel5.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.panel7.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.panel6.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ListBox listBox_LocalOnly;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ListBox listBox_RemoteOnly;
        private System.Windows.Forms.Splitter splitter2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Splitter splitter3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.ListView listView_Unmatch;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader7;
        private System.Windows.Forms.ColumnHeader columnHeader8;
        private System.Windows.Forms.ColumnHeader columnHeader10;
        private System.Windows.Forms.ColumnHeader columnHeader9;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.ListView listView_Match;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.ColumnHeader columnHeader6;
        private System.Windows.Forms.ColumnHeader columnHeader11;
        private System.Windows.Forms.Splitter splitter4;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.TreeView treeView_localDup;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.TreeView treeView_remoteDup;
        private System.Windows.Forms.Panel panel6;
        private System.Windows.Forms.Splitter splitter5;
        private System.Windows.Forms.Button button_SaveLocalList;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Button button_SaveRemoteList;
        private System.Windows.Forms.Button button_SaveUnmatchList;
        private System.Windows.Forms.Panel panel7;
        private System.Windows.Forms.Button button_SaveLocalDupList;
        private System.Windows.Forms.Button button_SaveRemoteDupList;
        private System.Windows.Forms.Button button_SaveMatchedList;
        private System.Windows.Forms.Button button_DownloadB;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button button_trashB;
        private System.Windows.Forms.Button button_trashA;
        private System.Windows.Forms.Button button_DownloadA;
    }
}