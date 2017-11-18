namespace TSviewCloud
{
    partial class FormSearch
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButton_selected = new System.Windows.Forms.RadioButton();
            this.button_SelectTree = new System.Windows.Forms.Button();
            this.textBox_SearchFolder = new System.Windows.Forms.TextBox();
            this.radioButton_SerachFolder = new System.Windows.Forms.RadioButton();
            this.radioButton_SearchAll = new System.Windows.Forms.RadioButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.radioButton_contain = new System.Windows.Forms.RadioButton();
            this.radioButton_endswith = new System.Windows.Forms.RadioButton();
            this.radioButton_startswith = new System.Windows.Forms.RadioButton();
            this.checkBox_case = new System.Windows.Forms.CheckBox();
            this.checkBox_regex = new System.Windows.Forms.CheckBox();
            this.comboBox_name = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBox_Under = new System.Windows.Forms.CheckBox();
            this.checkBox_Over = new System.Windows.Forms.CheckBox();
            this.numericUpDown_under = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown_over = new System.Windows.Forms.NumericUpDown();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.dateTimePicker_createdTo = new System.Windows.Forms.DateTimePicker();
            this.dateTimePicker_modifiedTo = new System.Windows.Forms.DateTimePicker();
            this.label5 = new System.Windows.Forms.Label();
            this.dateTimePicker_createdFrom = new System.Windows.Forms.DateTimePicker();
            this.label4 = new System.Windows.Forms.Label();
            this.dateTimePicker_modifiedFrom = new System.Windows.Forms.DateTimePicker();
            this.button_seach = new System.Windows.Forms.Button();
            this.button_cancel = new System.Windows.Forms.Button();
            this.button_showresult = new System.Windows.Forms.Button();
            this.label_result = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.radioButton_typeAll = new System.Windows.Forms.RadioButton();
            this.radioButton_typeFolder = new System.Windows.Forms.RadioButton();
            this.radioButton_typeFile = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_under)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_over)).BeginInit();
            this.groupBox4.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButton_selected);
            this.groupBox1.Controls.Add(this.button_SelectTree);
            this.groupBox1.Controls.Add(this.textBox_SearchFolder);
            this.groupBox1.Controls.Add(this.radioButton_SerachFolder);
            this.groupBox1.Controls.Add(this.radioButton_SearchAll);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(887, 77);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Target";
            // 
            // radioButton_selected
            // 
            this.radioButton_selected.AutoSize = true;
            this.radioButton_selected.Checked = true;
            this.radioButton_selected.Location = new System.Drawing.Point(12, 46);
            this.radioButton_selected.Name = "radioButton_selected";
            this.radioButton_selected.Size = new System.Drawing.Size(123, 19);
            this.radioButton_selected.TabIndex = 4;
            this.radioButton_selected.TabStop = true;
            this.radioButton_selected.Text = "Selected items";
            this.radioButton_selected.UseVisualStyleBackColor = true;
            // 
            // button_SelectTree
            // 
            this.button_SelectTree.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_SelectTree.Enabled = false;
            this.button_SelectTree.Location = new System.Drawing.Point(831, 42);
            this.button_SelectTree.Name = "button_SelectTree";
            this.button_SelectTree.Size = new System.Drawing.Size(33, 23);
            this.button_SelectTree.TabIndex = 3;
            this.button_SelectTree.Text = "...";
            this.button_SelectTree.UseVisualStyleBackColor = true;
            this.button_SelectTree.Click += new System.EventHandler(this.button_SelectTree_Click);
            // 
            // textBox_SearchFolder
            // 
            this.textBox_SearchFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_SearchFolder.Enabled = false;
            this.textBox_SearchFolder.Location = new System.Drawing.Point(160, 45);
            this.textBox_SearchFolder.Name = "textBox_SearchFolder";
            this.textBox_SearchFolder.Size = new System.Drawing.Size(656, 22);
            this.textBox_SearchFolder.TabIndex = 2;
            // 
            // radioButton_SerachFolder
            // 
            this.radioButton_SerachFolder.AutoSize = true;
            this.radioButton_SerachFolder.Location = new System.Drawing.Point(160, 20);
            this.radioButton_SerachFolder.Name = "radioButton_SerachFolder";
            this.radioButton_SerachFolder.Size = new System.Drawing.Size(142, 19);
            this.radioButton_SerachFolder.TabIndex = 1;
            this.radioButton_SerachFolder.Text = "Only in this folder";
            this.radioButton_SerachFolder.UseVisualStyleBackColor = true;
            this.radioButton_SerachFolder.CheckedChanged += new System.EventHandler(this.radioButton_SerachFolder_CheckedChanged);
            // 
            // radioButton_SearchAll
            // 
            this.radioButton_SearchAll.AutoSize = true;
            this.radioButton_SearchAll.Location = new System.Drawing.Point(12, 21);
            this.radioButton_SearchAll.Name = "radioButton_SearchAll";
            this.radioButton_SearchAll.Size = new System.Drawing.Size(95, 19);
            this.radioButton_SearchAll.TabIndex = 0;
            this.radioButton_SearchAll.Text = "All servers";
            this.radioButton_SearchAll.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.radioButton_contain);
            this.groupBox2.Controls.Add(this.radioButton_endswith);
            this.groupBox2.Controls.Add(this.radioButton_startswith);
            this.groupBox2.Controls.Add(this.checkBox_case);
            this.groupBox2.Controls.Add(this.checkBox_regex);
            this.groupBox2.Controls.Add(this.comboBox_name);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox2.Location = new System.Drawing.Point(0, 77);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(887, 126);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Search Item by Name";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(38, 48);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(172, 15);
            this.label3.TabIndex = 0;
            this.label3.Text = "Keep blank to hit all items";
            // 
            // radioButton_contain
            // 
            this.radioButton_contain.AutoSize = true;
            this.radioButton_contain.Checked = true;
            this.radioButton_contain.Location = new System.Drawing.Point(333, 101);
            this.radioButton_contain.Name = "radioButton_contain";
            this.radioButton_contain.Size = new System.Drawing.Size(84, 19);
            this.radioButton_contain.TabIndex = 5;
            this.radioButton_contain.TabStop = true;
            this.radioButton_contain.Text = "Contains";
            this.radioButton_contain.UseVisualStyleBackColor = true;
            // 
            // radioButton_endswith
            // 
            this.radioButton_endswith.AutoSize = true;
            this.radioButton_endswith.Location = new System.Drawing.Point(333, 76);
            this.radioButton_endswith.Name = "radioButton_endswith";
            this.radioButton_endswith.Size = new System.Drawing.Size(86, 19);
            this.radioButton_endswith.TabIndex = 4;
            this.radioButton_endswith.Text = "EndsWIth";
            this.radioButton_endswith.UseVisualStyleBackColor = true;
            // 
            // radioButton_startswith
            // 
            this.radioButton_startswith.AutoSize = true;
            this.radioButton_startswith.Location = new System.Drawing.Point(333, 51);
            this.radioButton_startswith.Name = "radioButton_startswith";
            this.radioButton_startswith.Size = new System.Drawing.Size(93, 19);
            this.radioButton_startswith.TabIndex = 3;
            this.radioButton_startswith.Text = "StartsWith";
            this.radioButton_startswith.UseVisualStyleBackColor = true;
            // 
            // checkBox_case
            // 
            this.checkBox_case.AutoSize = true;
            this.checkBox_case.Location = new System.Drawing.Point(551, 52);
            this.checkBox_case.Name = "checkBox_case";
            this.checkBox_case.Size = new System.Drawing.Size(104, 19);
            this.checkBox_case.TabIndex = 2;
            this.checkBox_case.Text = "Ignore case";
            this.checkBox_case.UseVisualStyleBackColor = true;
            this.checkBox_case.CheckedChanged += new System.EventHandler(this.checkBox_case_CheckedChanged);
            // 
            // checkBox_regex
            // 
            this.checkBox_regex.AutoSize = true;
            this.checkBox_regex.Location = new System.Drawing.Point(551, 27);
            this.checkBox_regex.Name = "checkBox_regex";
            this.checkBox_regex.Size = new System.Drawing.Size(256, 19);
            this.checkBox_regex.TabIndex = 1;
            this.checkBox_regex.Text = "Search text with regular expression";
            this.checkBox_regex.UseVisualStyleBackColor = true;
            this.checkBox_regex.CheckedChanged += new System.EventHandler(this.checkBox_regex_CheckedChanged);
            // 
            // comboBox_name
            // 
            this.comboBox_name.FormattingEnabled = true;
            this.comboBox_name.Location = new System.Drawing.Point(12, 21);
            this.comboBox_name.Name = "comboBox_name";
            this.comboBox_name.Size = new System.Drawing.Size(508, 23);
            this.comboBox_name.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.checkBox_Under);
            this.groupBox3.Controls.Add(this.checkBox_Over);
            this.groupBox3.Controls.Add(this.numericUpDown_under);
            this.groupBox3.Controls.Add(this.numericUpDown_over);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(0, 0);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(666, 100);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Search Item by Size";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(207, 53);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 15);
            this.label2.TabIndex = 5;
            this.label2.Text = "bytes";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(526, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 15);
            this.label1.TabIndex = 4;
            this.label1.Text = "bytes";
            // 
            // checkBox_Under
            // 
            this.checkBox_Under.AutoSize = true;
            this.checkBox_Under.Location = new System.Drawing.Point(350, 21);
            this.checkBox_Under.Name = "checkBox_Under";
            this.checkBox_Under.Size = new System.Drawing.Size(122, 19);
            this.checkBox_Under.TabIndex = 3;
            this.checkBox_Under.Text = "File size under";
            this.checkBox_Under.UseVisualStyleBackColor = true;
            // 
            // checkBox_Over
            // 
            this.checkBox_Over.AutoSize = true;
            this.checkBox_Over.Location = new System.Drawing.Point(35, 21);
            this.checkBox_Over.Name = "checkBox_Over";
            this.checkBox_Over.Size = new System.Drawing.Size(114, 19);
            this.checkBox_Over.TabIndex = 2;
            this.checkBox_Over.Text = "File size over";
            this.checkBox_Over.UseVisualStyleBackColor = true;
            // 
            // numericUpDown_under
            // 
            this.numericUpDown_under.Location = new System.Drawing.Point(350, 46);
            this.numericUpDown_under.Maximum = new decimal(new int[] {
            -1530494976,
            232830,
            0,
            0});
            this.numericUpDown_under.Name = "numericUpDown_under";
            this.numericUpDown_under.Size = new System.Drawing.Size(170, 22);
            this.numericUpDown_under.TabIndex = 1;
            this.numericUpDown_under.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.numericUpDown_under.ThousandsSeparator = true;
            // 
            // numericUpDown_over
            // 
            this.numericUpDown_over.Location = new System.Drawing.Point(31, 46);
            this.numericUpDown_over.Maximum = new decimal(new int[] {
            -1530494976,
            232830,
            0,
            0});
            this.numericUpDown_over.Name = "numericUpDown_over";
            this.numericUpDown_over.Size = new System.Drawing.Size(170, 22);
            this.numericUpDown_over.TabIndex = 0;
            this.numericUpDown_over.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.numericUpDown_over.ThousandsSeparator = true;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.dateTimePicker_createdTo);
            this.groupBox4.Controls.Add(this.dateTimePicker_modifiedTo);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.dateTimePicker_createdFrom);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Controls.Add(this.dateTimePicker_modifiedFrom);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox4.Location = new System.Drawing.Point(0, 303);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(887, 112);
            this.groupBox4.TabIndex = 3;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Search Item by Date";
            // 
            // dateTimePicker_createdTo
            // 
            this.dateTimePicker_createdTo.Checked = false;
            this.dateTimePicker_createdTo.CustomFormat = "yyyy-MM-dd (dddd) hh:mm:ss";
            this.dateTimePicker_createdTo.DropDownAlign = System.Windows.Forms.LeftRightAlignment.Right;
            this.dateTimePicker_createdTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker_createdTo.Location = new System.Drawing.Point(517, 70);
            this.dateTimePicker_createdTo.Name = "dateTimePicker_createdTo";
            this.dateTimePicker_createdTo.ShowCheckBox = true;
            this.dateTimePicker_createdTo.Size = new System.Drawing.Size(321, 22);
            this.dateTimePicker_createdTo.TabIndex = 5;
            // 
            // dateTimePicker_modifiedTo
            // 
            this.dateTimePicker_modifiedTo.Checked = false;
            this.dateTimePicker_modifiedTo.CustomFormat = "yyyy-MM-dd (dddd) hh:mm:ss";
            this.dateTimePicker_modifiedTo.DropDownAlign = System.Windows.Forms.LeftRightAlignment.Right;
            this.dateTimePicker_modifiedTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker_modifiedTo.Location = new System.Drawing.Point(517, 41);
            this.dateTimePicker_modifiedTo.Name = "dateTimePicker_modifiedTo";
            this.dateTimePicker_modifiedTo.ShowCheckBox = true;
            this.dateTimePicker_modifiedTo.Size = new System.Drawing.Size(321, 22);
            this.dateTimePicker_modifiedTo.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(18, 76);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(120, 15);
            this.label5.TabIndex = 3;
            this.label5.Text = "Item created time";
            // 
            // dateTimePicker_createdFrom
            // 
            this.dateTimePicker_createdFrom.Checked = false;
            this.dateTimePicker_createdFrom.CustomFormat = "yyyy-MM-dd (dddd) hh:mm:ss";
            this.dateTimePicker_createdFrom.DropDownAlign = System.Windows.Forms.LeftRightAlignment.Right;
            this.dateTimePicker_createdFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker_createdFrom.Location = new System.Drawing.Point(166, 70);
            this.dateTimePicker_createdFrom.Name = "dateTimePicker_createdFrom";
            this.dateTimePicker_createdFrom.ShowCheckBox = true;
            this.dateTimePicker_createdFrom.Size = new System.Drawing.Size(321, 22);
            this.dateTimePicker_createdFrom.TabIndex = 2;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(18, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(124, 15);
            this.label4.TabIndex = 1;
            this.label4.Text = "Item modified time";
            // 
            // dateTimePicker_modifiedFrom
            // 
            this.dateTimePicker_modifiedFrom.Checked = false;
            this.dateTimePicker_modifiedFrom.CustomFormat = "yyyy-MM-dd (dddd) hh:mm:ss";
            this.dateTimePicker_modifiedFrom.DropDownAlign = System.Windows.Forms.LeftRightAlignment.Right;
            this.dateTimePicker_modifiedFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker_modifiedFrom.Location = new System.Drawing.Point(166, 42);
            this.dateTimePicker_modifiedFrom.Name = "dateTimePicker_modifiedFrom";
            this.dateTimePicker_modifiedFrom.ShowCheckBox = true;
            this.dateTimePicker_modifiedFrom.Size = new System.Drawing.Size(321, 22);
            this.dateTimePicker_modifiedFrom.TabIndex = 0;
            // 
            // button_seach
            // 
            this.button_seach.Location = new System.Drawing.Point(721, 424);
            this.button_seach.Name = "button_seach";
            this.button_seach.Size = new System.Drawing.Size(127, 58);
            this.button_seach.TabIndex = 4;
            this.button_seach.Text = "Search";
            this.button_seach.UseVisualStyleBackColor = true;
            this.button_seach.Click += new System.EventHandler(this.button_seach_Click);
            // 
            // button_cancel
            // 
            this.button_cancel.Enabled = false;
            this.button_cancel.Location = new System.Drawing.Point(588, 433);
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.Size = new System.Drawing.Size(107, 40);
            this.button_cancel.TabIndex = 5;
            this.button_cancel.Text = "Cancel";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            // 
            // button_showresult
            // 
            this.button_showresult.Enabled = false;
            this.button_showresult.Location = new System.Drawing.Point(14, 435);
            this.button_showresult.Name = "button_showresult";
            this.button_showresult.Size = new System.Drawing.Size(104, 39);
            this.button_showresult.TabIndex = 6;
            this.button_showresult.Text = "Show Result";
            this.button_showresult.UseVisualStyleBackColor = true;
            this.button_showresult.Click += new System.EventHandler(this.button_showresult_Click);
            // 
            // label_result
            // 
            this.label_result.AutoSize = true;
            this.label_result.Location = new System.Drawing.Point(124, 433);
            this.label_result.Name = "label_result";
            this.label_result.Size = new System.Drawing.Size(0, 15);
            this.label_result.TabIndex = 7;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(127, 451);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(435, 23);
            this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar1.TabIndex = 8;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.groupBox3);
            this.panel1.Controls.Add(this.groupBox5);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 203);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(887, 100);
            this.panel1.TabIndex = 6;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.radioButton_typeFile);
            this.groupBox5.Controls.Add(this.radioButton_typeFolder);
            this.groupBox5.Controls.Add(this.radioButton_typeAll);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Right;
            this.groupBox5.Location = new System.Drawing.Point(666, 0);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(221, 100);
            this.groupBox5.TabIndex = 3;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Search Item by type";
            // 
            // radioButton_typeAll
            // 
            this.radioButton_typeAll.AutoSize = true;
            this.radioButton_typeAll.Checked = true;
            this.radioButton_typeAll.Location = new System.Drawing.Point(31, 21);
            this.radioButton_typeAll.Name = "radioButton_typeAll";
            this.radioButton_typeAll.Size = new System.Drawing.Size(43, 19);
            this.radioButton_typeAll.TabIndex = 0;
            this.radioButton_typeAll.TabStop = true;
            this.radioButton_typeAll.Text = "All";
            this.radioButton_typeAll.UseVisualStyleBackColor = true;
            // 
            // radioButton_typeFolder
            // 
            this.radioButton_typeFolder.AutoSize = true;
            this.radioButton_typeFolder.Location = new System.Drawing.Point(31, 46);
            this.radioButton_typeFolder.Name = "radioButton_typeFolder";
            this.radioButton_typeFolder.Size = new System.Drawing.Size(67, 19);
            this.radioButton_typeFolder.TabIndex = 1;
            this.radioButton_typeFolder.Text = "Folder";
            this.radioButton_typeFolder.UseVisualStyleBackColor = true;
            // 
            // radioButton_typeFile
            // 
            this.radioButton_typeFile.AutoSize = true;
            this.radioButton_typeFile.Location = new System.Drawing.Point(31, 71);
            this.radioButton_typeFile.Name = "radioButton_typeFile";
            this.radioButton_typeFile.Size = new System.Drawing.Size(50, 19);
            this.radioButton_typeFile.TabIndex = 2;
            this.radioButton_typeFile.Text = "File";
            this.radioButton_typeFile.UseVisualStyleBackColor = true;
            // 
            // FormSearch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(887, 500);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.label_result);
            this.Controls.Add(this.button_showresult);
            this.Controls.Add(this.button_cancel);
            this.Controls.Add(this.button_seach);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "FormSearch";
            this.Text = "Search Items";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_under)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_over)).EndInit();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button button_SelectTree;
        private System.Windows.Forms.TextBox textBox_SearchFolder;
        private System.Windows.Forms.RadioButton radioButton_SerachFolder;
        private System.Windows.Forms.RadioButton radioButton_SearchAll;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.RadioButton radioButton_endswith;
        private System.Windows.Forms.RadioButton radioButton_startswith;
        private System.Windows.Forms.CheckBox checkBox_case;
        private System.Windows.Forms.CheckBox checkBox_regex;
        private System.Windows.Forms.ComboBox comboBox_name;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.RadioButton radioButton_contain;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBox_Under;
        private System.Windows.Forms.CheckBox checkBox_Over;
        private System.Windows.Forms.NumericUpDown numericUpDown_under;
        private System.Windows.Forms.NumericUpDown numericUpDown_over;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.DateTimePicker dateTimePicker_modifiedFrom;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.DateTimePicker dateTimePicker_createdFrom;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button button_seach;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Button button_showresult;
        private System.Windows.Forms.Label label_result;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.RadioButton radioButton_selected;
        private System.Windows.Forms.DateTimePicker dateTimePicker_modifiedTo;
        private System.Windows.Forms.DateTimePicker dateTimePicker_createdTo;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.RadioButton radioButton_typeFile;
        private System.Windows.Forms.RadioButton radioButton_typeFolder;
        private System.Windows.Forms.RadioButton radioButton_typeAll;
    }
}