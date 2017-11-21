using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloud
{
    public partial class FormConfigEdit : Form
    {
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


        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public FormConfigEdit()
        {
            InitializeComponent();
            using (var g = CreateGraphics())
            {
                currentDpi = (int)g.DpiY;
            }
            HandleDpiChanged();
        }

        public int SelectedTabpage
        {
            get { return tabControl1.SelectedIndex; }
            set { tabControl1.SelectedIndex = value; }
        }

        private void LoadData()
        {
            checkBox_SaveCacheCompressed.Checked = TSviewCloudConfig.Config.SaveGZConfig;
            checkBox_EncryptConfig.Checked = TSviewCloudConfig.Config.SaveEncrypted;
            SetBandwidthInfo();
            textBox_UploadParallel.Text = TSviewCloudConfig.Config.ParallelUpload.ToString();
            textBox_DownloadParallel.Text = TSviewCloudConfig.Config.ParallelDownload.ToString();

            textBox_fontpath.Text = TSviewCloudConfig.ConfigFFplayer.FontFilePath;
            numericUpDown_FontPtSize.Value = TSviewCloudConfig.ConfigFFplayer.FontPtSize;
            checkBox_AutoResize.Checked = TSviewCloudConfig.ConfigFFplayer.AutoResize;
            foreach (var item in TSviewCloudConfig.ConfigFFplayer.FFmoduleKeybinds)
            {
                foreach (var akey in item.Value)
                {
                    var listitem = new ListViewItem(new string[] { "", "" });
                    listitem.SubItems[1].Text = akey.ToString();
                    switch (item.Key)
                    {
                        case ffmodule.FFplayerKeymapFunction.FuncPlayExit:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncPlayExit;
                            listitem.Text = Resource_text.FuncPlayExit_str;
                            listitem.ToolTipText = Resource_text.FuncPlayExit_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSeekMinus10sec:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSeekMinus10sec;
                            listitem.Text = Resource_text.FuncSeekMinus10sec_str;
                            listitem.ToolTipText = Resource_text.FuncSeekMinus10sec_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSeekMinus60sec:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSeekMinus60sec;
                            listitem.Text = Resource_text.FuncSeekMinus60sec_str;
                            listitem.ToolTipText = Resource_text.FuncSeekMinus60sec_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSeekPlus10sec:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSeekPlus10sec;
                            listitem.Text = Resource_text.FuncSeekPlus10sec_str;
                            listitem.ToolTipText = Resource_text.FuncSeekPlus10sec_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSeekPlus60sec:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSeekPlus60sec;
                            listitem.Text = Resource_text.FuncSeekPlus60sec_str;
                            listitem.ToolTipText = Resource_text.FuncSeekPlus60sec_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncToggleFullscreen:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncToggleFullscreen;
                            listitem.Text = Resource_text.FuncToggleFullscreen_str;
                            listitem.ToolTipText = Resource_text.FuncToggleFullscreen_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncVolumeDown:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncVolumeDown;
                            listitem.Text = Resource_text.FuncVolumeDown_str;
                            listitem.ToolTipText = Resource_text.FuncVolumeDown_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncVolumeUp:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncVolumeUp;
                            listitem.Text = Resource_text.FuncVolumeUp_str;
                            listitem.ToolTipText = Resource_text.FuncVolumeUp_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncToggleDisplay:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncToggleDisplay;
                            listitem.Text = Resource_text.FuncToggleDisplay_str;
                            listitem.ToolTipText = Resource_text.FuncToggleDisplay_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncToggleMute:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncToggleMute;
                            listitem.Text = Resource_text.FuncToggleMute_str;
                            listitem.ToolTipText = Resource_text.FuncToggleMute_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncCycleChannel:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncCycleChannel;
                            listitem.Text = Resource_text.FuncCycleChannel_str;
                            listitem.ToolTipText = Resource_text.FuncCycleChannel_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncCycleAudio:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncCycleAudio;
                            listitem.Text = Resource_text.FuncCycleAudio_str;
                            listitem.ToolTipText = Resource_text.FuncCycleAudio_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncCycleSubtitle:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncCycleSubtitle;
                            listitem.Text = Resource_text.FuncCycleSubtitle_str;
                            listitem.ToolTipText = Resource_text.FuncCycleSubtitle_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncForwardChapter:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncForwardChapter;
                            listitem.Text = Resource_text.FuncForwardChapter_str;
                            listitem.ToolTipText = Resource_text.FuncForwardChapter_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncRewindChapter:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncRewindChapter;
                            listitem.Text = Resource_text.FuncRewindChapter_str;
                            listitem.ToolTipText = Resource_text.FuncRewindChapter_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncTogglePause:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncTogglePause;
                            listitem.Text = Resource_text.FuncTogglePause_str;
                            listitem.ToolTipText = Resource_text.FuncTogglePause_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncResizeOriginal:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncResizeOriginal;
                            listitem.Text = Resource_text.FuncResizeOriginal_str;
                            listitem.ToolTipText = Resource_text.FuncResizeOriginal_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSrcVolumeUp:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSrcVolumeUp;
                            listitem.Text = Resource_text.FuncSrcVolumeUp_str;
                            listitem.ToolTipText = Resource_text.FuncSrcVolumeUp_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSrcVolumeDown:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSrcVolumeDown;
                            listitem.Text = Resource_text.FuncSrcVolumeDown_str;
                            listitem.ToolTipText = Resource_text.FuncSrcVolumeDown_tip_str;
                            break;
                        case ffmodule.FFplayerKeymapFunction.FuncSrcAutoVolume:
                            listitem.Tag = ffmodule.FFplayerKeymapFunction.FuncSrcAutoVolume;
                            listitem.Text = Resource_text.FuncSrcAutoVolume_str;
                            listitem.ToolTipText = Resource_text.FuncSrcAutoVolume_tip_str;
                            break;
                        default:
                            continue;
                    }
                    listView1.Items.Add(listitem);
                }
            }
            listView1.Sorting = SortOrder.Ascending;
            listView1.Sort();

            textBox_SendHost.Text = TSviewCloudConfig.ConfigTSsend.SendToHost;
            textBox_SendPort.Text = TSviewCloudConfig.ConfigTSsend.SendToPort.ToString();
            textBox_SendPacketNum.Text = TSviewCloudConfig.ConfigTSsend.SendPacketNum.ToString();
            textBox_SendDelay.Text = TSviewCloudConfig.ConfigTSsend.SendDelay.ToString();
            textBox_SendLongOffset.Text = TSviewCloudConfig.ConfigTSsend.SendLongOffset.ToString();
            textBox_SendRatebySendCount.Text = TSviewCloudConfig.ConfigTSsend.SendRatebySendCount.ToString();
            textBox_SendRatebyTOTCount.Text = TSviewCloudConfig.ConfigTSsend.SendRatebyTOTCount.ToString();
            textBox_VK.Text = TSviewCloudConfig.ConfigTSsend.SendVK.ToString();
            SendVK = TSviewCloudConfig.ConfigTSsend.SendVK;
            textBox_keySendApp.Text = TSviewCloudConfig.ConfigTSsend.SendVK_Application;
        }

        private void SaveData()
        {
            TSviewCloudConfig.Config.SaveGZConfig = checkBox_SaveCacheCompressed.Checked;
            TSviewCloudConfig.Config.SaveEncrypted = checkBox_EncryptConfig.Checked;

            if (comboBox_UploadLimitUnit.SelectedIndex == comboBox_UploadLimitUnit.Items.IndexOf("Infinity"))
            {
                TSviewCloudConfig.Config.UploadLimit = double.PositiveInfinity;
            }
            else
            {
                try
                {
                    double value = double.Parse(textBox_UploadBandwidthLimit.Text);
                    TSviewCloudConfig.Config.UploadLimit = ConvertUnit(value, (string)comboBox_UploadLimitUnit.SelectedItem);
                }
                catch { }
            }
            if (comboBox_DownloadLimitUnit.SelectedIndex == comboBox_DownloadLimitUnit.Items.IndexOf("Infinity"))
            {
                TSviewCloudConfig.Config.DownloadLimit = double.PositiveInfinity;
            }
            else
            {
                try
                {
                    double value = double.Parse(textBox_DownloadBandwidthLimit.Text);
                    TSviewCloudConfig.Config.DownloadLimit = ConvertUnit(value, (string)comboBox_DownloadLimitUnit.SelectedItem);
                }
                catch { }
            }

            try
            {
                TSviewCloudConfig.Config.ParallelUpload = int.Parse(textBox_UploadParallel.Text);
            }
            catch
            {
                TSviewCloudConfig.Config.ParallelUpload = 1;
            }
            try
            {
                TSviewCloudConfig.Config.ParallelDownload = int.Parse(textBox_DownloadParallel.Text);
            }
            catch
            {
                TSviewCloudConfig.Config.ParallelDownload = 1;
            }

            if (File.Exists(textBox_fontpath.Text))
            {
                TSviewCloudConfig.ConfigFFplayer.FontFilePath = textBox_fontpath.Text;
            }
            TSviewCloudConfig.ConfigFFplayer.FontPtSize = (int)numericUpDown_FontPtSize.Value;
            TSviewCloudConfig.ConfigFFplayer.AutoResize = checkBox_AutoResize.Checked;

            TSviewCloudConfig.ConfigFFplayer.FFmoduleKeybinds.Clear();
            var keyconverter = new KeysConverter();
            foreach (ListViewItem item in listView1.Items)
            {
                var command = (ffmodule.FFplayerKeymapFunction)(item.Tag);
                TSviewCloudConfig.FFmoduleKeysClass key_array;
                TSviewCloudConfig.ConfigFFplayer.FFmoduleKeybinds.TryGetValue(command, out key_array);
                key_array = key_array ?? new TSviewCloudConfig.FFmoduleKeysClass();
                key_array.Add((Keys)(keyconverter.ConvertFromString(item.SubItems[1].Text)));
                TSviewCloudConfig.ConfigFFplayer.FFmoduleKeybinds[command] = key_array;
            }

            TSviewCloudConfig.ConfigTSsend.SendToHost = textBox_SendHost.Text;
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendToPort = int.Parse(textBox_SendPort.Text);
            }
            catch { }
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendPacketNum = int.Parse(textBox_SendPacketNum.Text);
            }
            catch { }
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendDelay = int.Parse(textBox_SendDelay.Text);
            }
            catch { }
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendLongOffset = int.Parse(textBox_SendLongOffset.Text);
            }
            catch { }
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendRatebySendCount = int.Parse(textBox_SendRatebySendCount.Text);
            }
            catch { }
            try
            {
                TSviewCloudConfig.ConfigTSsend.SendRatebyTOTCount = int.Parse(textBox_SendRatebyTOTCount.Text);
            }
            catch { }
            TSviewCloudConfig.ConfigTSsend.SendVK = SendVK;
            TSviewCloudConfig.ConfigTSsend.SendVK_Application = textBox_keySendApp.Text;

            TSviewCloudConfig.Config.Save();
        }

        private double ConvertUnit(double value, string Unit)
        {
            switch (Unit)
            {
                case "GiB/s":
                    return value * 1024 * 1024 * 1024;
                case "MiB/s":
                    return value * 1024 * 1024;
                case "KiB/s":
                    return value * 1024;
                case "GB/s":
                    return value * 1000 * 1000 * 1000;
                case "MB/s":
                    return value * 1000 * 1000;
                case "KB/s":
                    return value * 1000;
            }
            return value;
        }

        private int SelectBase(double value)
        {
            double value10 = value / 1000;
            double value2 = value / 1024;
            if (value10 < 1000 || value2 < 1024)
            {
                if (value10 % 1.0 == 0)
                {
                    return 10;
                }
                if (value2 % 1.0 == 0)
                {
                    return 2;
                }

                if (value10 % 0.1 == 0)
                {
                    return 10;
                }
                if (value2 % 0.1 == 0)
                {
                    return 2;
                }

                if (value10 % 0.01 == 0)
                {
                    return 10;
                }
                if (value2 % 0.01 == 0)
                {
                    return 2;
                }

                return 2;
            }
            else
            {
                if (SelectBase(value10) == 10) return 10;
                else return 2;
            }
        }

        private void SetBandwidthInfo()
        {
            if (double.IsPositiveInfinity(TSviewCloudConfig.Config.UploadLimit) || TSviewCloudConfig.Config.UploadLimit <= 0)
            {
                TSviewCloudConfig.Config.UploadLimit = double.PositiveInfinity;
                textBox_UploadBandwidthLimit.Text = "";
                comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("Infinity");
            }
            else
            {
                double value = TSviewCloudConfig.Config.UploadLimit;
                if (value < 1000)
                {
                    textBox_UploadBandwidthLimit.Text = value.ToString();
                    comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("Byte/s");
                }
                else
                {
                    if (SelectBase(value) == 10)
                    {
                        if (value > 1000 * 1000 * 1000)
                        {
                            textBox_UploadBandwidthLimit.Text = (value / (1000 * 1000 * 1000)).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("GB/s");
                        }
                        else if (value > 1000 * 1000)
                        {
                            textBox_UploadBandwidthLimit.Text = (value / (1000 * 1000)).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("MB/s");
                        }
                        else
                        {
                            textBox_UploadBandwidthLimit.Text = (value / 1000).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("KB/s");
                        }
                    }
                    else
                    {
                        if (value > 1024 * 1024 * 1024)
                        {
                            textBox_UploadBandwidthLimit.Text = (value / (1024 * 1024 * 1024)).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("GiB/s");
                        }
                        else if (value > 1024 * 1024)
                        {
                            textBox_UploadBandwidthLimit.Text = (value / (1024 * 1024)).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("MiB/s");
                        }
                        else
                        {
                            textBox_UploadBandwidthLimit.Text = (value / 1024).ToString();
                            comboBox_UploadLimitUnit.SelectedIndex = comboBox_UploadLimitUnit.Items.IndexOf("KiB/s");
                        }
                    }
                }
            }

            if (double.IsPositiveInfinity(TSviewCloudConfig.Config.DownloadLimit) || TSviewCloudConfig.Config.DownloadLimit <= 0)
            {
                TSviewCloudConfig.Config.DownloadLimit = double.PositiveInfinity;
                textBox_DownloadBandwidthLimit.Text = "";
                comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("Infinity");
            }
            else
            {
                double value = TSviewCloudConfig.Config.DownloadLimit;
                if (value < 1000)
                {
                    textBox_DownloadBandwidthLimit.Text = value.ToString();
                    comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("Byte/s");
                }
                else
                {
                    if (SelectBase(value) == 10)
                    {
                        if (value > 1000 * 1000 * 1000)
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / (1000 * 1000 * 1000)).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("GB/s");
                        }
                        else if (value > 1000 * 1000)
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / (1000 * 1000)).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("MB/s");
                        }
                        else
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / 1000).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("KB/s");
                        }
                    }
                    else
                    {
                        if (value > 1024 * 1024 * 1024)
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / (1024 * 1024 * 1024)).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("GiB/s");
                        }
                        else if (value > 1024 * 1024)
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / (1024 * 1024)).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("MiB/s");
                        }
                        else
                        {
                            textBox_DownloadBandwidthLimit.Text = (value / 1024).ToString();
                            comboBox_DownloadLimitUnit.SelectedIndex = comboBox_DownloadLimitUnit.Items.IndexOf("KiB/s");
                        }
                    }
                }
            }
        }


        private void FormConfigEdit_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void button_OK_Click(object sender, EventArgs e)
        {
            SaveData();
        }

        private void button_FontSelect_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = textBox_fontpath.Text;
            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox_fontpath.Text = openFileDialog1.FileName;
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                var item = listView1.SelectedItems[0];
                textBox_key.Text = item.SubItems[1].Text;
            }
        }

        private void textBox_key_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            textBox_key.Text = e.KeyCode.ToString();
            if (listView1.SelectedItems.Count > 0)
            {
                var item = listView1.SelectedItems[0];
                item.SubItems[1].Text = textBox_key.Text;
            }
        }

        private void listView1_Click(object sender, EventArgs e)
        {
            textBox_key.Focus();
        }

        private void deleteKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                var item = listView1.SelectedItems[0];
                item.SubItems[1].Text = Keys.None.ToString();
            }
        }

        private void button_MasterPass_Click(object sender, EventArgs e)
        {
            if (TSviewCloudConfig.Config.IsMasterPasswordCorrect)
            {
                using (var f = new FormMasterPass())
                    f.ShowDialog(this);

            }
        }

        private void comboBox_UploadLimitUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_UploadLimitUnit.SelectedIndex == comboBox_UploadLimitUnit.Items.IndexOf("Infinity"))
            {
                textBox_UploadBandwidthLimit.Text = "";
            }
        }

        private void comboBox_DownloadLimitUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_DownloadLimitUnit.SelectedIndex == comboBox_DownloadLimitUnit.Items.IndexOf("Infinity"))
            {
                textBox_DownloadBandwidthLimit.Text = "";
            }
        }

        private void textBox_UploadBandwidthLimit_TextChanged(object sender, EventArgs e)
        {
            try
            {
                double value = double.Parse(textBox_UploadBandwidthLimit.Text);
            }
            catch
            {
                textBox_UploadBandwidthLimit.Text = "";
            }
        }

        private void textBox_DownloadBandwidthLimit_TextChanged(object sender, EventArgs e)
        {
            try
            {
                double value = double.Parse(textBox_DownloadBandwidthLimit.Text);
            }
            catch
            {
                textBox_DownloadBandwidthLimit.Text = "";
            }
        }

        Keys SendVK;

        private void textBox_VK_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            textBox_VK.Text = e.KeyCode.ToString();
            SendVK = e.KeyCode;
        }
    }
}
