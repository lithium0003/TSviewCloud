namespace TSviewCloud
{
    partial class FormMatch
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMatch));
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButton_HashMD5 = new System.Windows.Forms.RadioButton();
            this.radioButton_HashNone = new System.Windows.Forms.RadioButton();
            this.radioButton_Hash = new System.Windows.Forms.RadioButton();
            this.radioButton_filename = new System.Windows.Forms.RadioButton();
            this.radioButton_Tree = new System.Windows.Forms.RadioButton();
            this.button_clearRemote = new System.Windows.Forms.Button();
            this.button_clearLocal = new System.Windows.Forms.Button();
            this.button_AddRemote = new System.Windows.Forms.Button();
            this.button_cancel = new System.Windows.Forms.Button();
            this.label_info = new System.Windows.Forms.Label();
            this.button_start = new System.Windows.Forms.Button();
            this.button_AddFolder = new System.Windows.Forms.Button();
            this.button_AddFile = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.listBox_remote = new System.Windows.Forms.ListBox();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.listBox_local = new System.Windows.Forms.ListBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deltetItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBox1);
            this.panel1.Controls.Add(this.radioButton_Hash);
            this.panel1.Controls.Add(this.radioButton_filename);
            this.panel1.Controls.Add(this.radioButton_Tree);
            this.panel1.Controls.Add(this.button_clearRemote);
            this.panel1.Controls.Add(this.button_clearLocal);
            this.panel1.Controls.Add(this.button_AddRemote);
            this.panel1.Controls.Add(this.button_cancel);
            this.panel1.Controls.Add(this.label_info);
            this.panel1.Controls.Add(this.button_start);
            this.panel1.Controls.Add(this.button_AddFolder);
            this.panel1.Controls.Add(this.button_AddFile);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButton_HashMD5);
            this.groupBox1.Controls.Add(this.radioButton_HashNone);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // radioButton_HashMD5
            // 
            resources.ApplyResources(this.radioButton_HashMD5, "radioButton_HashMD5");
            this.radioButton_HashMD5.Name = "radioButton_HashMD5";
            this.radioButton_HashMD5.UseVisualStyleBackColor = true;
            // 
            // radioButton_HashNone
            // 
            resources.ApplyResources(this.radioButton_HashNone, "radioButton_HashNone");
            this.radioButton_HashNone.Checked = true;
            this.radioButton_HashNone.Name = "radioButton_HashNone";
            this.radioButton_HashNone.TabStop = true;
            this.radioButton_HashNone.UseVisualStyleBackColor = true;
            this.radioButton_HashNone.CheckedChanged += new System.EventHandler(this.radioButton_HashNone_CheckedChanged);
            // 
            // radioButton_Hash
            // 
            resources.ApplyResources(this.radioButton_Hash, "radioButton_Hash");
            this.radioButton_Hash.Name = "radioButton_Hash";
            this.radioButton_Hash.UseVisualStyleBackColor = true;
            // 
            // radioButton_filename
            // 
            resources.ApplyResources(this.radioButton_filename, "radioButton_filename");
            this.radioButton_filename.Name = "radioButton_filename";
            this.radioButton_filename.UseVisualStyleBackColor = true;
            // 
            // radioButton_Tree
            // 
            resources.ApplyResources(this.radioButton_Tree, "radioButton_Tree");
            this.radioButton_Tree.Checked = true;
            this.radioButton_Tree.Name = "radioButton_Tree";
            this.radioButton_Tree.TabStop = true;
            this.radioButton_Tree.UseVisualStyleBackColor = true;
            // 
            // button_clearRemote
            // 
            resources.ApplyResources(this.button_clearRemote, "button_clearRemote");
            this.button_clearRemote.Name = "button_clearRemote";
            this.button_clearRemote.UseVisualStyleBackColor = true;
            this.button_clearRemote.Click += new System.EventHandler(this.button_clearRemote_Click);
            // 
            // button_clearLocal
            // 
            resources.ApplyResources(this.button_clearLocal, "button_clearLocal");
            this.button_clearLocal.Name = "button_clearLocal";
            this.button_clearLocal.UseVisualStyleBackColor = true;
            this.button_clearLocal.Click += new System.EventHandler(this.button_clearLocal_Click);
            // 
            // button_AddRemote
            // 
            resources.ApplyResources(this.button_AddRemote, "button_AddRemote");
            this.button_AddRemote.Name = "button_AddRemote";
            this.toolTip1.SetToolTip(this.button_AddRemote, resources.GetString("button_AddRemote.ToolTip"));
            this.button_AddRemote.UseVisualStyleBackColor = true;
            this.button_AddRemote.Click += new System.EventHandler(this.button_AddRemote_Click);
            // 
            // button_cancel
            // 
            this.button_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.button_cancel, "button_cancel");
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            // 
            // label_info
            // 
            resources.ApplyResources(this.label_info, "label_info");
            this.label_info.Name = "label_info";
            // 
            // button_start
            // 
            resources.ApplyResources(this.button_start, "button_start");
            this.button_start.Name = "button_start";
            this.button_start.UseVisualStyleBackColor = true;
            this.button_start.Click += new System.EventHandler(this.button_start_Click);
            // 
            // button_AddFolder
            // 
            resources.ApplyResources(this.button_AddFolder, "button_AddFolder");
            this.button_AddFolder.Name = "button_AddFolder";
            this.toolTip1.SetToolTip(this.button_AddFolder, resources.GetString("button_AddFolder.ToolTip"));
            this.button_AddFolder.UseVisualStyleBackColor = true;
            this.button_AddFolder.Click += new System.EventHandler(this.button_AddFolder_Click);
            // 
            // button_AddFile
            // 
            resources.ApplyResources(this.button_AddFile, "button_AddFile");
            this.button_AddFile.Name = "button_AddFile";
            this.toolTip1.SetToolTip(this.button_AddFile, resources.GetString("button_AddFile.ToolTip"));
            this.button_AddFile.UseVisualStyleBackColor = true;
            this.button_AddFile.Click += new System.EventHandler(this.button_AddFile_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.listBox_remote);
            this.panel2.Controls.Add(this.splitter1);
            this.panel2.Controls.Add(this.listBox_local);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // listBox_remote
            // 
            this.listBox_remote.AllowDrop = true;
            this.listBox_remote.ContextMenuStrip = this.contextMenuStrip2;
            resources.ApplyResources(this.listBox_remote, "listBox_remote");
            this.listBox_remote.FormattingEnabled = true;
            this.listBox_remote.Name = "listBox_remote";
            this.listBox_remote.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_remote.Sorted = true;
            this.listBox_remote.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.listBox_remote_Format);
            this.listBox_remote.DragDrop += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragDrop);
            this.listBox_remote.DragEnter += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragEnter);
            this.listBox_remote.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listBox_remote_KeyDown);
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1});
            this.contextMenuStrip2.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip2, "contextMenuStrip2");
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(this.toolStripMenuItem1, "toolStripMenuItem1");
            this.toolStripMenuItem1.Click += new System.EventHandler(this.toolStripMenuItem1_Click);
            // 
            // splitter1
            // 
            resources.ApplyResources(this.splitter1, "splitter1");
            this.splitter1.Name = "splitter1";
            this.splitter1.TabStop = false;
            // 
            // listBox_local
            // 
            this.listBox_local.AllowDrop = true;
            this.listBox_local.ContextMenuStrip = this.contextMenuStrip1;
            resources.ApplyResources(this.listBox_local, "listBox_local");
            this.listBox_local.FormattingEnabled = true;
            this.listBox_local.Name = "listBox_local";
            this.listBox_local.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_local.Sorted = true;
            this.listBox_local.DragDrop += new System.Windows.Forms.DragEventHandler(this.listBox_local_DragDrop);
            this.listBox_local.DragEnter += new System.Windows.Forms.DragEventHandler(this.listBox_local_DragEnter);
            this.listBox_local.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listBox1_KeyDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deltetItemToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // deltetItemToolStripMenuItem
            // 
            this.deltetItemToolStripMenuItem.Name = "deltetItemToolStripMenuItem";
            resources.ApplyResources(this.deltetItemToolStripMenuItem, "deltetItemToolStripMenuItem");
            this.deltetItemToolStripMenuItem.Click += new System.EventHandler(this.deltetItemToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Multiselect = true;
            // 
            // FormMatch
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "FormMatch";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMatch_FormClosing);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.contextMenuStrip2.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button_AddFolder;
        private System.Windows.Forms.Button button_AddFile;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ListBox listBox_local;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Label label_info;
        private System.Windows.Forms.Button button_start;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deltetItemToolStripMenuItem;
        private System.Windows.Forms.Button button_AddRemote;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.ListBox listBox_remote;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button button_clearRemote;
        private System.Windows.Forms.Button button_clearLocal;
        private System.Windows.Forms.RadioButton radioButton_Hash;
        private System.Windows.Forms.RadioButton radioButton_filename;
        private System.Windows.Forms.RadioButton radioButton_Tree;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton_HashMD5;
        private System.Windows.Forms.RadioButton radioButton_HashNone;
    }
}