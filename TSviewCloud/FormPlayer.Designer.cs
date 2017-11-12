namespace TSviewCloud
{
    partial class FormPlayer
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
            this.trackBar_Possition = new System.Windows.Forms.TrackBar();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button_Close = new System.Windows.Forms.Button();
            this.button_Prev = new System.Windows.Forms.Button();
            this.label_nextfile = new System.Windows.Forms.Label();
            this.comboBox_PlayerType = new System.Windows.Forms.ComboBox();
            this.button_config = new System.Windows.Forms.Button();
            this.textBox_Duration = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBox_StartSkip = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.button_Next = new System.Windows.Forms.Button();
            this.button_Stop = new System.Windows.Forms.Button();
            this.button_Play = new System.Windows.Forms.Button();
            this.label_Filename = new System.Windows.Forms.Label();
            this.label_TIme = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.timer2 = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Possition)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // trackBar_Possition
            // 
            this.trackBar_Possition.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.trackBar_Possition.Location = new System.Drawing.Point(0, 104);
            this.trackBar_Possition.Name = "trackBar_Possition";
            this.trackBar_Possition.Size = new System.Drawing.Size(710, 56);
            this.trackBar_Possition.TabIndex = 0;
            this.trackBar_Possition.TickFrequency = 100;
            this.trackBar_Possition.ValueChanged += new System.EventHandler(this.trackBar_Possition_ValueChanged);
            this.trackBar_Possition.MouseCaptureChanged += new System.EventHandler(this.trackBar_Possition_MouseCaptureChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.button_Close);
            this.panel1.Controls.Add(this.button_Prev);
            this.panel1.Controls.Add(this.label_nextfile);
            this.panel1.Controls.Add(this.comboBox_PlayerType);
            this.panel1.Controls.Add(this.button_config);
            this.panel1.Controls.Add(this.textBox_Duration);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.textBox_StartSkip);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.button_Next);
            this.panel1.Controls.Add(this.button_Stop);
            this.panel1.Controls.Add(this.button_Play);
            this.panel1.Controls.Add(this.label_Filename);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(710, 98);
            this.panel1.TabIndex = 1;
            // 
            // button_Close
            // 
            this.button_Close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button_Close.Location = new System.Drawing.Point(632, 5);
            this.button_Close.Name = "button_Close";
            this.button_Close.Size = new System.Drawing.Size(75, 23);
            this.button_Close.TabIndex = 13;
            this.button_Close.Text = "Close";
            this.button_Close.UseVisualStyleBackColor = true;
            this.button_Close.Click += new System.EventHandler(this.button_Close_Click);
            // 
            // button_Prev
            // 
            this.button_Prev.Location = new System.Drawing.Point(111, 38);
            this.button_Prev.Name = "button_Prev";
            this.button_Prev.Size = new System.Drawing.Size(63, 23);
            this.button_Prev.TabIndex = 12;
            this.button_Prev.Text = "< Prev";
            this.button_Prev.UseVisualStyleBackColor = true;
            this.button_Prev.Click += new System.EventHandler(this.button_Prev_Click);
            // 
            // label_nextfile
            // 
            this.label_nextfile.AutoSize = true;
            this.label_nextfile.Location = new System.Drawing.Point(4, 76);
            this.label_nextfile.Name = "label_nextfile";
            this.label_nextfile.Size = new System.Drawing.Size(43, 15);
            this.label_nextfile.TabIndex = 11;
            this.label_nextfile.Text = "label1";
            // 
            // comboBox_PlayerType
            // 
            this.comboBox_PlayerType.DisplayMember = "0";
            this.comboBox_PlayerType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_PlayerType.Enabled = false;
            this.comboBox_PlayerType.FormattingEnabled = true;
            this.comboBox_PlayerType.Items.AddRange(new object[] {
            "FFmpeg",
            "TS send"});
            this.comboBox_PlayerType.Location = new System.Drawing.Point(261, 38);
            this.comboBox_PlayerType.Name = "comboBox_PlayerType";
            this.comboBox_PlayerType.Size = new System.Drawing.Size(121, 23);
            this.comboBox_PlayerType.TabIndex = 10;
            // 
            // button_config
            // 
            this.button_config.Location = new System.Drawing.Point(624, 62);
            this.button_config.Name = "button_config";
            this.button_config.Size = new System.Drawing.Size(75, 23);
            this.button_config.TabIndex = 9;
            this.button_config.Text = "Config";
            this.button_config.UseVisualStyleBackColor = true;
            this.button_config.Click += new System.EventHandler(this.button_config_Click);
            // 
            // textBox_Duration
            // 
            this.textBox_Duration.Location = new System.Drawing.Point(509, 63);
            this.textBox_Duration.Name = "textBox_Duration";
            this.textBox_Duration.Size = new System.Drawing.Size(100, 22);
            this.textBox_Duration.TabIndex = 8;
            this.textBox_Duration.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBox_Duration_KeyPress);
            this.textBox_Duration.Leave += new System.EventHandler(this.textBox_Duration_Leave);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(397, 66);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 15);
            this.label2.TabIndex = 7;
            this.label2.Text = "PlayDuration";
            // 
            // textBox_StartSkip
            // 
            this.textBox_StartSkip.Location = new System.Drawing.Point(509, 35);
            this.textBox_StartSkip.Name = "textBox_StartSkip";
            this.textBox_StartSkip.Size = new System.Drawing.Size(100, 22);
            this.textBox_StartSkip.TabIndex = 6;
            this.textBox_StartSkip.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBox_StartSkip_KeyPress);
            this.textBox_StartSkip.Leave += new System.EventHandler(this.textBox_StartSkip_Leave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(397, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(95, 15);
            this.label1.TabIndex = 5;
            this.label1.Text = "StartSkipTime";
            // 
            // button_Next
            // 
            this.button_Next.Location = new System.Drawing.Point(180, 38);
            this.button_Next.Name = "button_Next";
            this.button_Next.Size = new System.Drawing.Size(63, 23);
            this.button_Next.TabIndex = 4;
            this.button_Next.Text = "Next >";
            this.button_Next.UseVisualStyleBackColor = true;
            this.button_Next.Click += new System.EventHandler(this.button_Next_Click);
            // 
            // button_Stop
            // 
            this.button_Stop.BackgroundImage = global::TSviewCloud.Properties.Resources.stop;
            this.button_Stop.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button_Stop.Location = new System.Drawing.Point(53, 38);
            this.button_Stop.Name = "button_Stop";
            this.button_Stop.Size = new System.Drawing.Size(40, 35);
            this.button_Stop.TabIndex = 3;
            this.button_Stop.UseVisualStyleBackColor = true;
            this.button_Stop.Click += new System.EventHandler(this.button_Stop_Click);
            // 
            // button_Play
            // 
            this.button_Play.BackgroundImage = global::TSviewCloud.Properties.Resources.play1;
            this.button_Play.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button_Play.Location = new System.Drawing.Point(7, 38);
            this.button_Play.Name = "button_Play";
            this.button_Play.Size = new System.Drawing.Size(40, 35);
            this.button_Play.TabIndex = 2;
            this.button_Play.UseVisualStyleBackColor = true;
            this.button_Play.Click += new System.EventHandler(this.button_Play_Click);
            // 
            // label_Filename
            // 
            this.label_Filename.AutoSize = true;
            this.label_Filename.Location = new System.Drawing.Point(4, 9);
            this.label_Filename.Name = "label_Filename";
            this.label_Filename.Size = new System.Drawing.Size(43, 15);
            this.label_Filename.TabIndex = 1;
            this.label_Filename.Text = "label1";
            // 
            // label_TIme
            // 
            this.label_TIme.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label_TIme.AutoSize = true;
            this.label_TIme.Location = new System.Drawing.Point(3, 136);
            this.label_TIme.Name = "label_TIme";
            this.label_TIme.Size = new System.Drawing.Size(43, 15);
            this.label_TIme.TabIndex = 0;
            this.label_TIme.Text = "label1";
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // timer2
            // 
            this.timer2.Interval = 500;
            this.timer2.Tick += new System.EventHandler(this.timer2_Tick);
            // 
            // FormPlayer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(710, 160);
            this.Controls.Add(this.label_TIme);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.trackBar_Possition);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormPlayer";
            this.Text = "FormPlayer";
            this.Load += new System.EventHandler(this.FormPlayer_Load);
            ((System.ComponentModel.ISupportInitialize)(this.trackBar_Possition)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TrackBar trackBar_Possition;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button_Play;
        private System.Windows.Forms.Label label_Filename;
        private System.Windows.Forms.Label label_TIme;
        private System.Windows.Forms.ComboBox comboBox_PlayerType;
        private System.Windows.Forms.Button button_config;
        private System.Windows.Forms.TextBox textBox_Duration;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBox_StartSkip;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button_Next;
        private System.Windows.Forms.Button button_Stop;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label label_nextfile;
        private System.Windows.Forms.Button button_Prev;
        private System.Windows.Forms.Button button_Close;
        private System.Windows.Forms.Timer timer2;
    }
}