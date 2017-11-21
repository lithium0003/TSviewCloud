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

namespace TSviewCloud
{
    public partial class FormPlayer : Form
    {
        public FormPlayer()
        {
            InitializeComponent();
            StreamDuration = null;
            StreamPossition = null;
            CurrentFile = "";
            NextFile = null;
        }

        public class SeekEventArgs : EventArgs
        {
            public double NewPossition;
        }
        public delegate void SeekEventHandler(object sender, SeekEventArgs e);

        private static readonly SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        public enum PlayerTypes {
            FFmpeg,
            TSsend,
        };
        public PlayerTypes PlayerType
        {
            get
            {
                switch (comboBox_PlayerType.Text)
                {
                    case "FFmpeg":
                        return PlayerTypes.FFmpeg;
                    case "TS send":
                        return PlayerTypes.TSsend;
                    default:
                        return PlayerTypes.FFmpeg;
                }
            }
            set
            {
                switch (value)
                {
                    case PlayerTypes.FFmpeg:
                        comboBox_PlayerType.SelectedItem = comboBox_PlayerType.Items[0];
                        break;
                    case PlayerTypes.TSsend:
                        comboBox_PlayerType.SelectedItem = comboBox_PlayerType.Items[1];
                        break;
                }
            }
        }

        bool _IsPlaying;
        bool _IsDone;
        double _StartDelay = double.NaN;
        double _Duration = double.NaN;
        string _CurrentFile;
        string _NextFile;
        double? _StreamDuration;
        double? _StreamPossition;
        int _PlayIndex;
        EventHandler _StartDelayChanged;
        EventHandler _DurationChanged;
        EventHandler _PositionRequest;
        EventHandler _StartCallback;
        EventHandler _StopCallback;
        EventHandler _NextCallback;
        EventHandler _PrevCallback;
        SeekEventHandler _SeekCallback;

        public void ClearCallback()
        {
            if (StartDelayChanged != null)
            {
                foreach (Delegate d in StartDelayChanged.GetInvocationList())
                {
                    StartDelayChanged -= (EventHandler)d;
                }
            }
            if (DurationChanged != null)
            {
                foreach (Delegate d in DurationChanged.GetInvocationList())
                {
                    DurationChanged -= (EventHandler)d;
                }
            }
            if (PositionRequest != null)
            {
                foreach (Delegate d in PositionRequest.GetInvocationList())
                {
                    PositionRequest -= (EventHandler)d;
                }
            }
            if (StartCallback != null)
            {
                foreach (Delegate d in StartCallback.GetInvocationList())
                {
                    StartCallback -= (EventHandler)d;
                }
            }
            if (StopCallback != null)
            {
                foreach (Delegate d in StopCallback.GetInvocationList())
                {
                    StopCallback -= (EventHandler)d;
                }
            }
            if (NextCallback != null)
            {
                foreach (Delegate d in NextCallback.GetInvocationList())
                {
                    NextCallback -= (EventHandler)d;
                }
            }
            if (PrevCallback != null)
            {
                foreach (Delegate d in PrevCallback.GetInvocationList())
                {
                    PrevCallback -= (EventHandler)d;
                }
            }
        }

        public void Start()
        {
            _IsDone = false;
            StartCallback?.Invoke(this, new EventArgs());
            if (_IsDone) return;

            _IsPlaying = true;
            synchronizationContext.Post((o) => {
                timer1.Enabled = true;
            }, null);
        }

        public void Stop()
        {
            StopCallback?.Invoke(this, new EventArgs());
            _IsPlaying = false;
            _IsDone = true;
            synchronizationContext.Post((o) => {
                timer1.Enabled = false;
            }, null);
        }

        public void Done()
        {
            _IsDone = true;
            synchronizationContext.Post((o) => {
                timer1.Enabled = false;
                if(_IsPlaying)
                    Hide();
                _IsPlaying = false;
            }, null);
        }

        public void Next()
        {
            NextCallback?.Invoke(this, new EventArgs());
        }

        public void Prev()
        {
            PlayIndex--;
            PrevCallback?.Invoke(this, new EventArgs());
        }

        public void Seek(double NewPossition)
        {
            SeekCallback?.Invoke(this, new SeekEventArgs() { NewPossition = NewPossition });
        }

        public double StartDelay {
            get => _StartDelay;
            set {
                if(_StartDelay != value)
                {
                    StartDelayChanged?.Invoke(this, new EventArgs());
                }
                _StartDelay = value;
                synchronizationContext.Post((o) => {
                    textBox_StartSkip.Text = (double.IsNaN(StartDelay)) ? "" : TimeSpan.FromSeconds(StartDelay).ToString();
                }, null);
            }
        }
        public double Duration {
            get => _Duration;
            set
            {
                if(_Duration != value)
                {
                    DurationChanged?.Invoke(this, new EventArgs());
                }
                _Duration = value;
                synchronizationContext.Post((o) => {
                    textBox_Duration.Text = (double.IsNaN(Duration)) ? "" : TimeSpan.FromSeconds(Duration).ToString();
                }, null);
            }
        }
        public string CurrentFile {
            get => _CurrentFile;
            set
            {
                _CurrentFile = value;
                synchronizationContext.Post((o) => {
                    label_Filename.Text = _CurrentFile;
                }, null);
            }
        }
        public string NextFile {
            get => _NextFile;
            set
            {
                _NextFile = value;
                synchronizationContext.Post((o) => {
                    if (string.IsNullOrEmpty(_NextFile))
                    {
                        label_nextfile.Text = "end of playlist";
                    }
                    else
                    {
                        label_nextfile.Text = "Next : " + _NextFile;
                    }
                }, null);
            }
        }
        public double? StreamDuration {
            get => _StreamDuration;
            set
            {
                _StreamDuration = value;
                synchronizationContext.Post((o) => {
                    if (_StreamDuration == null)
                    {
                        trackBar_Possition.Enabled = false;
                        label_TIme.Text = "";
                    }
                    else
                    {
                        trackBar_Possition.Enabled = true;
                        SetTrackBarPossition();
                    }
                }, null);
            }
        }
        public double? StreamPossition {
            get => _StreamPossition;
            set
            {
                _StreamPossition = value;
                synchronizationContext.Post((o) => {
                    if (_StreamPossition == null)
                    {
                        trackBar_Possition.Enabled = false;
                        label_TIme.Text = "";
                    }
                    else
                    {
                        trackBar_Possition.Enabled = true;
                        SetTrackBarPossition();
                    }
                }, null);
            }
        }


        public EventHandler StartDelayChanged { get => _StartDelayChanged; set => _StartDelayChanged = value; }
        public EventHandler DurationChanged { get => _DurationChanged; set => _DurationChanged = value; }
        public EventHandler PositionRequest { get => _PositionRequest; set => _PositionRequest = value; }
        public EventHandler StartCallback { get => _StartCallback; set => _StartCallback = value; }
        public EventHandler StopCallback { get => _StopCallback; set => _StopCallback = value; }
        public EventHandler NextCallback { get => _NextCallback; set => _NextCallback = value; }
        public EventHandler PrevCallback { get => _PrevCallback; set => _PrevCallback = value; }
        public SeekEventHandler SeekCallback { get => _SeekCallback; set => _SeekCallback = value; }
        public bool IsPlaying { get => _IsPlaying; }
        public int PlayIndex
        {
            get => _PlayIndex;
            set
            {
                if (value < -1) _PlayIndex = -1;
                else _PlayIndex = value;
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (TSviewCloudConfig.Config.ApplicationExit) return;
            e.Cancel = true;
            Hide();
        }

        protected override void OnShown(EventArgs e)
        {
            Screen disp = Screen.PrimaryScreen;

            Point p = new Point(disp.WorkingArea.Right - Width, disp.WorkingArea.Height - Height);
            Location = p;
            base.OnShown(e);
        }


        private void FormPlayer_Load(object sender, EventArgs e)
        {
            comboBox_PlayerType.SelectedIndex = (comboBox_PlayerType.SelectedIndex < 0)? 0: comboBox_PlayerType.SelectedIndex;
            trackBar_Possition.Maximum = 1000000;
            trackBar_Possition.TickFrequency = 1000000;

            Screen disp = Screen.PrimaryScreen;

            Point p = new Point(disp.WorkingArea.Right - Width, disp.WorkingArea.Height - Height);
            Location = p;
        }

        private void ProcessStartSkip()
        {
            if (textBox_StartSkip.Text == "")
                StartDelay = double.NaN;
            else
            {
                try
                {
                    StartDelay = double.Parse(textBox_StartSkip.Text);
                }
                catch
                {
                    try
                    {
                        StartDelay = TimeSpan.Parse(textBox_StartSkip.Text).TotalSeconds;
                    }
                    catch
                    {
                        StartDelay = double.NaN;
                    }
                }
            }
        }

        private void ProcessDuration()
        {
            if (textBox_Duration.Text == "")
                StartDelay = double.NaN;
            else
            {
                try
                {
                    Duration = double.Parse(textBox_Duration.Text);
                }
                catch
                {
                    try
                    {
                        Duration = TimeSpan.Parse(textBox_Duration.Text).TotalSeconds;
                    }
                    catch
                    {
                        Duration = double.NaN;
                    }
                }
            }
        }

        private void textBox_StartSkip_Leave(object sender, EventArgs e)
        {
            ProcessStartSkip();
        }

        private void textBox_StartSkip_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                ProcessStartSkip();
                e.Handled = true;
            }
        }

        private void textBox_Duration_Leave(object sender, EventArgs e)
        {
            ProcessDuration();
        }

        private void textBox_Duration_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                ProcessDuration();
                e.Handled = true;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            PositionRequest?.Invoke(this, new EventArgs());
        }

        private void button_Play_Click(object sender, EventArgs e)
        {
            Start();
        }

        private void button_Stop_Click(object sender, EventArgs e)
        {
            Stop();
        }

        private void button_Next_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void button_Prev_Click(object sender, EventArgs e)
        {
            Prev();
        }

        private void SetTrackBarPossition()
        {
            try
            {
                double duration = StreamDuration ?? 1;
                if(duration < 10)
                {
                    trackBar_Possition.TickFrequency = (int)(trackBar_Possition.Maximum / duration);
                }
                else if (duration < 60)
                {
                    trackBar_Possition.TickFrequency = (int)(trackBar_Possition.Maximum / duration * 5);
                }
                else if (duration < 600)
                {
                    trackBar_Possition.TickFrequency = (int)(trackBar_Possition.Maximum / duration * 30);
                }
                else if (duration < 3600)
                {
                    trackBar_Possition.TickFrequency = (int)(trackBar_Possition.Maximum / duration * 60);
                }
                else
                {
                    trackBar_Possition.TickFrequency = (int)(trackBar_Possition.Maximum / duration * 600);
                }
                var value = (int)((StreamPossition ?? 0) / (StreamDuration ?? 1) * trackBar_Possition.Maximum);
                trackBar_Possition.Tag = 1;
                trackBar_Possition.Value = (value < trackBar_Possition.Minimum) ? 0 : (value > trackBar_Possition.Maximum) ? trackBar_Possition.Maximum : value;
                trackBar_Possition.Tag = 0;
                label_TIme.Text = string.Format("{0} / {1}",
                    TimeSpan.FromSeconds(StreamPossition ?? 0).ToString(@"hh\:mm\:ss\.fff"),
                    TimeSpan.FromSeconds(StreamDuration ?? 0).ToString(@"hh\:mm\:ss"));
            }
            catch { }
        }

        private void button_Close_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void trackBar_Possition_MouseCaptureChanged(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            timer2.Enabled = true;
        }

        private void trackBar_Possition_ValueChanged(object sender, EventArgs e)
        {
            if (trackBar_Possition.Tag as int? == 1) return;
            timer2.Enabled = false;
            timer1.Enabled = false;
            label_TIme.Text = string.Format("seek to {0} / {1}",
                TimeSpan.FromSeconds((double)trackBar_Possition.Value / trackBar_Possition.Maximum * (StreamDuration ?? 1)).ToString(@"hh\:mm\:ss\.fff"),
                TimeSpan.FromSeconds(StreamDuration ?? 0).ToString(@"hh\:mm\:ss"));
            timer2.Enabled = true;

        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            timer2.Enabled = false;
            var val = (double)trackBar_Possition.Value / trackBar_Possition.Maximum * (StreamDuration ?? 1);
            Seek(val);
            timer1.Enabled = true;
        }

        private void button_config_Click(object sender, EventArgs e)
        {
            (new FormConfigEdit() { SelectedTabpage = 1 }).ShowDialog(this);
        }
    }
}
