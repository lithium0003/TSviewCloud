namespace TSviewCloud
{
    partial class FormDiff
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormDiff));
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_AddRemoteA = new System.Windows.Forms.Button();
            this.radioButton_Hash = new System.Windows.Forms.RadioButton();
            this.radioButton_filename = new System.Windows.Forms.RadioButton();
            this.radioButton_Tree = new System.Windows.Forms.RadioButton();
            this.button_clearB = new System.Windows.Forms.Button();
            this.button_clearA = new System.Windows.Forms.Button();
            this.button_AddRemoteB = new System.Windows.Forms.Button();
            this.button_cancel = new System.Windows.Forms.Button();
            this.label_info = new System.Windows.Forms.Label();
            this.button_start = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.listBox_B = new System.Windows.Forms.ListBox();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.listBox_A = new System.Windows.Forms.ListBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deltetItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.button_AddRemoteA);
            this.panel1.Controls.Add(this.radioButton_Hash);
            this.panel1.Controls.Add(this.radioButton_filename);
            this.panel1.Controls.Add(this.radioButton_Tree);
            this.panel1.Controls.Add(this.button_clearB);
            this.panel1.Controls.Add(this.button_clearA);
            this.panel1.Controls.Add(this.button_AddRemoteB);
            this.panel1.Controls.Add(this.button_cancel);
            this.panel1.Controls.Add(this.label_info);
            this.panel1.Controls.Add(this.button_start);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // button_AddRemoteA
            // 
            resources.ApplyResources(this.button_AddRemoteA, "button_AddRemoteA");
            this.button_AddRemoteA.Name = "button_AddRemoteA";
            this.button_AddRemoteA.UseVisualStyleBackColor = true;
            this.button_AddRemoteA.Click += new System.EventHandler(this.button_AddRemoteA_Click);
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
            // button_clearB
            // 
            resources.ApplyResources(this.button_clearB, "button_clearB");
            this.button_clearB.Name = "button_clearB";
            this.button_clearB.UseVisualStyleBackColor = true;
            this.button_clearB.Click += new System.EventHandler(this.button_clearRemoteB_Click);
            // 
            // button_clearA
            // 
            resources.ApplyResources(this.button_clearA, "button_clearA");
            this.button_clearA.Name = "button_clearA";
            this.button_clearA.UseVisualStyleBackColor = true;
            this.button_clearA.Click += new System.EventHandler(this.button_clearRemoteA_Click);
            // 
            // button_AddRemoteB
            // 
            resources.ApplyResources(this.button_AddRemoteB, "button_AddRemoteB");
            this.button_AddRemoteB.Name = "button_AddRemoteB";
            this.button_AddRemoteB.UseVisualStyleBackColor = true;
            this.button_AddRemoteB.Click += new System.EventHandler(this.button_AddRemoteB_Click);
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
            // panel2
            // 
            this.panel2.Controls.Add(this.listBox_B);
            this.panel2.Controls.Add(this.splitter1);
            this.panel2.Controls.Add(this.listBox_A);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // listBox_B
            // 
            this.listBox_B.AllowDrop = true;
            this.listBox_B.ContextMenuStrip = this.contextMenuStrip2;
            resources.ApplyResources(this.listBox_B, "listBox_B");
            this.listBox_B.FormattingEnabled = true;
            this.listBox_B.Name = "listBox_B";
            this.listBox_B.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_B.Sorted = true;
            this.listBox_B.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.listBox_remote_Format);
            this.listBox_B.DragDrop += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragDrop);
            this.listBox_B.DragEnter += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragEnter);
            this.listBox_B.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listBox_remote_KeyDown);
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
            this.toolStripMenuItem1.Click += new System.EventHandler(this.toolStripMenuItemB_Click);
            // 
            // splitter1
            // 
            resources.ApplyResources(this.splitter1, "splitter1");
            this.splitter1.Name = "splitter1";
            this.splitter1.TabStop = false;
            // 
            // listBox_A
            // 
            this.listBox_A.AllowDrop = true;
            this.listBox_A.ContextMenuStrip = this.contextMenuStrip1;
            resources.ApplyResources(this.listBox_A, "listBox_A");
            this.listBox_A.FormattingEnabled = true;
            this.listBox_A.Name = "listBox_A";
            this.listBox_A.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBox_A.Sorted = true;
            this.listBox_A.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.listBox_remote_Format);
            this.listBox_A.DragDrop += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragDrop);
            this.listBox_A.DragEnter += new System.Windows.Forms.DragEventHandler(this.listBox_remote_DragEnter);
            this.listBox_A.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listBox_remote_KeyDown);
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
            this.deltetItemToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItemA_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Multiselect = true;
            // 
            // FormDiff
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "FormDiff";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMatch_FormClosing);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.contextMenuStrip2.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ListBox listBox_A;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Label label_info;
        private System.Windows.Forms.Button button_start;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deltetItemToolStripMenuItem;
        private System.Windows.Forms.Button button_AddRemoteB;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.ListBox listBox_B;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button button_clearB;
        private System.Windows.Forms.Button button_clearA;
        private System.Windows.Forms.RadioButton radioButton_Hash;
        private System.Windows.Forms.RadioButton radioButton_filename;
        private System.Windows.Forms.RadioButton radioButton_Tree;
        private System.Windows.Forms.Button button_AddRemoteA;
    }
}