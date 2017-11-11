namespace TSviewCloud
{
    partial class FormConfigEdit
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_Cancel = new System.Windows.Forms.Button();
            this.button_OK = new System.Windows.Forms.Button();
            this.tabPage_general = new System.Windows.Forms.TabPage();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.textBox_DownloadParallel = new System.Windows.Forms.TextBox();
            this.textBox_UploadParallel = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.comboBox_DownloadLimitUnit = new System.Windows.Forms.ComboBox();
            this.comboBox_UploadLimitUnit = new System.Windows.Forms.ComboBox();
            this.textBox_DownloadBandwidthLimit = new System.Windows.Forms.TextBox();
            this.textBox_UploadBandwidthLimit = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.button_MasterPass = new System.Windows.Forms.Button();
            this.checkBox_EncryptConfig = new System.Windows.Forms.CheckBox();
            this.checkBox_SaveCacheCompressed = new System.Windows.Forms.CheckBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage_FFplayer = new System.Windows.Forms.TabPage();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.checkBox_AutoResize = new System.Windows.Forms.CheckBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteKeybindToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox_key = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.numericUpDown_FontPtSize = new System.Windows.Forms.NumericUpDown();
            this.button_FontSelect = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox_fontpath = new System.Windows.Forms.TextBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.panel1.SuspendLayout();
            this.tabPage_general.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage_FFplayer.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_FontPtSize)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.Controls.Add(this.button_Cancel);
            this.panel1.Controls.Add(this.button_OK);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 537);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(573, 53);
            this.panel1.TabIndex = 1;
            // 
            // button_Cancel
            // 
            this.button_Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button_Cancel.Location = new System.Drawing.Point(467, 18);
            this.button_Cancel.Name = "button_Cancel";
            this.button_Cancel.Size = new System.Drawing.Size(75, 23);
            this.button_Cancel.TabIndex = 1;
            this.button_Cancel.Text = "Cancel";
            this.button_Cancel.UseVisualStyleBackColor = true;
            // 
            // button_OK
            // 
            this.button_OK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button_OK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button_OK.Location = new System.Drawing.Point(26, 18);
            this.button_OK.Name = "button_OK";
            this.button_OK.Size = new System.Drawing.Size(75, 23);
            this.button_OK.TabIndex = 0;
            this.button_OK.Text = "OK";
            this.button_OK.UseVisualStyleBackColor = true;
            this.button_OK.Click += new System.EventHandler(this.button_OK_Click);
            // 
            // tabPage_general
            // 
            this.tabPage_general.Controls.Add(this.groupBox5);
            this.tabPage_general.Controls.Add(this.groupBox1);
            this.tabPage_general.Location = new System.Drawing.Point(4, 25);
            this.tabPage_general.Name = "tabPage_general";
            this.tabPage_general.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_general.Size = new System.Drawing.Size(565, 508);
            this.tabPage_general.TabIndex = 1;
            this.tabPage_general.Text = "General";
            this.tabPage_general.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.label10);
            this.groupBox5.Controls.Add(this.label9);
            this.groupBox5.Controls.Add(this.textBox_DownloadParallel);
            this.groupBox5.Controls.Add(this.textBox_UploadParallel);
            this.groupBox5.Controls.Add(this.label8);
            this.groupBox5.Controls.Add(this.label7);
            this.groupBox5.Controls.Add(this.comboBox_DownloadLimitUnit);
            this.groupBox5.Controls.Add(this.comboBox_UploadLimitUnit);
            this.groupBox5.Controls.Add(this.textBox_DownloadBandwidthLimit);
            this.groupBox5.Controls.Add(this.textBox_UploadBandwidthLimit);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox5.Location = new System.Drawing.Point(3, 99);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(559, 161);
            this.groupBox5.TabIndex = 2;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Network";
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(293, 93);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(153, 15);
            this.label10.TabIndex = 9;
            this.label10.Text = "Parallel download tasks";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(9, 93);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(135, 15);
            this.label9.TabIndex = 8;
            this.label9.Text = "Parallel upload tasks";
            // 
            // textBox_DownloadParallel
            // 
            this.textBox_DownloadParallel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_DownloadParallel.Location = new System.Drawing.Point(423, 114);
            this.textBox_DownloadParallel.Name = "textBox_DownloadParallel";
            this.textBox_DownloadParallel.Size = new System.Drawing.Size(100, 22);
            this.textBox_DownloadParallel.TabIndex = 7;
            // 
            // textBox_UploadParallel
            // 
            this.textBox_UploadParallel.Location = new System.Drawing.Point(133, 114);
            this.textBox_UploadParallel.Name = "textBox_UploadParallel";
            this.textBox_UploadParallel.Size = new System.Drawing.Size(100, 22);
            this.textBox_UploadParallel.TabIndex = 6;
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(293, 27);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(165, 15);
            this.label8.TabIndex = 5;
            this.label8.Text = "Download bandwidth limit";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(9, 27);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(146, 15);
            this.label7.TabIndex = 4;
            this.label7.Text = "Upload bandwidth limit";
            // 
            // comboBox_DownloadLimitUnit
            // 
            this.comboBox_DownloadLimitUnit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBox_DownloadLimitUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_DownloadLimitUnit.FormattingEnabled = true;
            this.comboBox_DownloadLimitUnit.Items.AddRange(new object[] {
            "Infinity",
            "Byte/s",
            "KiB/s",
            "KB/s",
            "MiB/s",
            "MB/s",
            "GiB/s",
            "GB/s"});
            this.comboBox_DownloadLimitUnit.Location = new System.Drawing.Point(402, 47);
            this.comboBox_DownloadLimitUnit.Name = "comboBox_DownloadLimitUnit";
            this.comboBox_DownloadLimitUnit.Size = new System.Drawing.Size(121, 23);
            this.comboBox_DownloadLimitUnit.TabIndex = 3;
            this.comboBox_DownloadLimitUnit.SelectedIndexChanged += new System.EventHandler(this.comboBox_DownloadLimitUnit_SelectedIndexChanged);
            // 
            // comboBox_UploadLimitUnit
            // 
            this.comboBox_UploadLimitUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_UploadLimitUnit.FormattingEnabled = true;
            this.comboBox_UploadLimitUnit.Items.AddRange(new object[] {
            "Infinity",
            "Byte/s",
            "KiB/s",
            "KB/s",
            "MiB/s",
            "MB/s",
            "GiB/s",
            "GB/s"});
            this.comboBox_UploadLimitUnit.Location = new System.Drawing.Point(112, 48);
            this.comboBox_UploadLimitUnit.Name = "comboBox_UploadLimitUnit";
            this.comboBox_UploadLimitUnit.Size = new System.Drawing.Size(121, 23);
            this.comboBox_UploadLimitUnit.TabIndex = 2;
            this.comboBox_UploadLimitUnit.SelectedIndexChanged += new System.EventHandler(this.comboBox_UploadLimitUnit_SelectedIndexChanged);
            // 
            // textBox_DownloadBandwidthLimit
            // 
            this.textBox_DownloadBandwidthLimit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_DownloadBandwidthLimit.Location = new System.Drawing.Point(296, 48);
            this.textBox_DownloadBandwidthLimit.Name = "textBox_DownloadBandwidthLimit";
            this.textBox_DownloadBandwidthLimit.Size = new System.Drawing.Size(100, 22);
            this.textBox_DownloadBandwidthLimit.TabIndex = 1;
            this.textBox_DownloadBandwidthLimit.TextChanged += new System.EventHandler(this.textBox_DownloadBandwidthLimit_TextChanged);
            // 
            // textBox_UploadBandwidthLimit
            // 
            this.textBox_UploadBandwidthLimit.Location = new System.Drawing.Point(6, 48);
            this.textBox_UploadBandwidthLimit.Name = "textBox_UploadBandwidthLimit";
            this.textBox_UploadBandwidthLimit.Size = new System.Drawing.Size(100, 22);
            this.textBox_UploadBandwidthLimit.TabIndex = 0;
            this.textBox_UploadBandwidthLimit.TextChanged += new System.EventHandler(this.textBox_UploadBandwidthLimit_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.button_MasterPass);
            this.groupBox1.Controls.Add(this.checkBox_EncryptConfig);
            this.groupBox1.Controls.Add(this.checkBox_SaveCacheCompressed);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(559, 96);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Server Configuration";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.Location = new System.Drawing.Point(307, 9);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(246, 44);
            this.label6.TabIndex = 3;
            this.label6.Text = "If you set MasterPassword, need to enter it on lanch app.";
            // 
            // button_MasterPass
            // 
            this.button_MasterPass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_MasterPass.Location = new System.Drawing.Point(366, 56);
            this.button_MasterPass.Name = "button_MasterPass";
            this.button_MasterPass.Size = new System.Drawing.Size(187, 23);
            this.button_MasterPass.TabIndex = 2;
            this.button_MasterPass.Text = "Change MasterPassword";
            this.button_MasterPass.UseVisualStyleBackColor = true;
            this.button_MasterPass.Click += new System.EventHandler(this.button_MasterPass_Click);
            // 
            // checkBox_EncryptConfig
            // 
            this.checkBox_EncryptConfig.AutoSize = true;
            this.checkBox_EncryptConfig.Location = new System.Drawing.Point(6, 46);
            this.checkBox_EncryptConfig.Name = "checkBox_EncryptConfig";
            this.checkBox_EncryptConfig.Size = new System.Drawing.Size(152, 19);
            this.checkBox_EncryptConfig.TabIndex = 1;
            this.checkBox_EncryptConfig.Text = "Encrypt config data";
            this.checkBox_EncryptConfig.UseVisualStyleBackColor = true;
            // 
            // checkBox_SaveCacheCompressed
            // 
            this.checkBox_SaveCacheCompressed.AutoSize = true;
            this.checkBox_SaveCacheCompressed.Location = new System.Drawing.Point(6, 21);
            this.checkBox_SaveCacheCompressed.Name = "checkBox_SaveCacheCompressed";
            this.checkBox_SaveCacheCompressed.Size = new System.Drawing.Size(216, 19);
            this.checkBox_SaveCacheCompressed.TabIndex = 0;
            this.checkBox_SaveCacheCompressed.Text = "Save cache data compressed";
            this.checkBox_SaveCacheCompressed.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage_general);
            this.tabControl1.Controls.Add(this.tabPage_FFplayer);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(573, 537);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage_FFplayer
            // 
            this.tabPage_FFplayer.Controls.Add(this.groupBox4);
            this.tabPage_FFplayer.Controls.Add(this.groupBox3);
            this.tabPage_FFplayer.Controls.Add(this.groupBox2);
            this.tabPage_FFplayer.Location = new System.Drawing.Point(4, 25);
            this.tabPage_FFplayer.Name = "tabPage_FFplayer";
            this.tabPage_FFplayer.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_FFplayer.Size = new System.Drawing.Size(565, 508);
            this.tabPage_FFplayer.TabIndex = 2;
            this.tabPage_FFplayer.Text = "FFplayer";
            this.tabPage_FFplayer.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Controls.Add(this.checkBox_AutoResize);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox4.Location = new System.Drawing.Point(332, 103);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(230, 99);
            this.groupBox4.TabIndex = 2;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "DisplaySize";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.Location = new System.Drawing.Point(6, 18);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(218, 36);
            this.label4.TabIndex = 1;
            this.label4.Text = "Filt window size to video automatially";
            // 
            // checkBox_AutoResize
            // 
            this.checkBox_AutoResize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBox_AutoResize.AutoSize = true;
            this.checkBox_AutoResize.Location = new System.Drawing.Point(122, 69);
            this.checkBox_AutoResize.Name = "checkBox_AutoResize";
            this.checkBox_AutoResize.Size = new System.Drawing.Size(102, 19);
            this.checkBox_AutoResize.TabIndex = 0;
            this.checkBox_AutoResize.Text = "Auto resize";
            this.checkBox_AutoResize.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.listView1);
            this.groupBox3.Controls.Add(this.panel2);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Left;
            this.groupBox3.Location = new System.Drawing.Point(3, 103);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(329, 402);
            this.groupBox3.TabIndex = 1;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Key bind";
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.listView1.ContextMenuStrip = this.contextMenuStrip1;
            this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView1.FullRowSelect = true;
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(3, 88);
            this.listView1.MultiSelect = false;
            this.listView1.Name = "listView1";
            this.listView1.ShowItemToolTips = true;
            this.listView1.Size = new System.Drawing.Size(323, 311);
            this.listView1.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);
            this.listView1.Click += new System.EventHandler(this.listView1_Click);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Function";
            this.columnHeader1.Width = 180;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Key";
            this.columnHeader2.Width = 100;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteKeybindToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(213, 28);
            // 
            // deleteKeybindToolStripMenuItem
            // 
            this.deleteKeybindToolStripMenuItem.Name = "deleteKeybindToolStripMenuItem";
            this.deleteKeybindToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteKeybindToolStripMenuItem.Size = new System.Drawing.Size(212, 24);
            this.deleteKeybindToolStripMenuItem.Text = "&Delete Keybind";
            this.deleteKeybindToolStripMenuItem.Click += new System.EventHandler(this.deleteKeybindToolStripMenuItem_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.label5);
            this.panel2.Controls.Add(this.textBox_key);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(3, 18);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(323, 70);
            this.panel2.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(3, 14);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(168, 48);
            this.label5.TabIndex = 2;
            this.label5.Text = "Press any key in textbox to select key bind ->";
            // 
            // textBox_key
            // 
            this.textBox_key.AcceptsReturn = true;
            this.textBox_key.AcceptsTab = true;
            this.textBox_key.Location = new System.Drawing.Point(177, 24);
            this.textBox_key.Name = "textBox_key";
            this.textBox_key.Size = new System.Drawing.Size(95, 22);
            this.textBox_key.TabIndex = 1;
            this.textBox_key.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBox_key_KeyDown);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.numericUpDown_FontPtSize);
            this.groupBox2.Controls.Add(this.button_FontSelect);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.textBox_fontpath);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox2.Location = new System.Drawing.Point(3, 3);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(559, 100);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Display Font";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(168, 75);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(19, 15);
            this.label3.TabIndex = 5;
            this.label3.Text = "pt";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 70);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 15);
            this.label2.TabIndex = 4;
            this.label2.Text = "Font size";
            // 
            // numericUpDown_FontPtSize
            // 
            this.numericUpDown_FontPtSize.Location = new System.Drawing.Point(88, 68);
            this.numericUpDown_FontPtSize.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.numericUpDown_FontPtSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_FontPtSize.Name = "numericUpDown_FontPtSize";
            this.numericUpDown_FontPtSize.Size = new System.Drawing.Size(74, 22);
            this.numericUpDown_FontPtSize.TabIndex = 3;
            this.numericUpDown_FontPtSize.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // button_FontSelect
            // 
            this.button_FontSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_FontSelect.Location = new System.Drawing.Point(523, 36);
            this.button_FontSelect.Name = "button_FontSelect";
            this.button_FontSelect.Size = new System.Drawing.Size(30, 23);
            this.button_FontSelect.TabIndex = 2;
            this.button_FontSelect.Text = "...";
            this.button_FontSelect.UseVisualStyleBackColor = true;
            this.button_FontSelect.Click += new System.EventHandler(this.button_FontSelect_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(112, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "Path to Font file";
            // 
            // textBox_fontpath
            // 
            this.textBox_fontpath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_fontpath.Location = new System.Drawing.Point(6, 36);
            this.textBox_fontpath.Name = "textBox_fontpath";
            this.textBox_fontpath.Size = new System.Drawing.Size(511, 22);
            this.textBox_fontpath.TabIndex = 0;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // FormConfigEdit
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(573, 590);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panel1);
            this.Name = "FormConfigEdit";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "FormConfigEdit";
            this.Load += new System.EventHandler(this.FormConfigEdit_Load);
            this.panel1.ResumeLayout(false);
            this.tabPage_general.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPage_FFplayer.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_FontPtSize)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button_Cancel;
        private System.Windows.Forms.Button button_OK;
        private System.Windows.Forms.TabPage tabPage_general;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox checkBox_SaveCacheCompressed;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage_FFplayer;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox checkBox_AutoResize;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox_key;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numericUpDown_FontPtSize;
        private System.Windows.Forms.Button button_FontSelect;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox_fontpath;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteKeybindToolStripMenuItem;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button button_MasterPass;
        private System.Windows.Forms.CheckBox checkBox_EncryptConfig;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.ComboBox comboBox_DownloadLimitUnit;
        private System.Windows.Forms.ComboBox comboBox_UploadLimitUnit;
        private System.Windows.Forms.TextBox textBox_DownloadBandwidthLimit;
        private System.Windows.Forms.TextBox textBox_UploadBandwidthLimit;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBox_DownloadParallel;
        private System.Windows.Forms.TextBox textBox_UploadParallel;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
    }
}