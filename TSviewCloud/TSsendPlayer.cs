using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TSviewCloudConfig;

namespace TSviewCloud
{
    class TSsendPlayer
    {
        private static readonly SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        private TimeSpan SendDuration;
        private TimeSpan SendStartDelay;
        private TimeSpan SeekUDPtoPos = TimeSpan.FromDays(100);
        private DateTime SendStartTime;

        private double _duration;
        private double _playtime;
        private DateTime _playDateTime;

        CancellationTokenSource seekUDP_ct_source = new CancellationTokenSource();

        private void CancelForSeekUDP()
        {
            var t = seekUDP_ct_source;
            seekUDP_ct_source = new CancellationTokenSource();
            t.Cancel();
        }


        public double StartSkip
        {
            get
            {
                if (SendStartDelay == default(TimeSpan)) return double.NaN;
                return SendStartDelay.TotalSeconds;
            }
            set {
                if (double.IsNaN(value)) SendStartDelay = default(TimeSpan);
                else SendStartDelay = TimeSpan.FromSeconds(value);
            }
        }
        public double StopDuration
        {
            get
            {
                if (SendDuration == default(TimeSpan)) return double.NaN;
                return SendDuration.TotalSeconds;
            }
            set
            {
                if (double.IsNaN(value)) SendDuration = default(TimeSpan);
                else SendDuration = TimeSpan.FromSeconds(value);
            }
        }
        public double Duration
        {
            get
            {
                return _duration;
            }
        }
        public double PlayTime
        {
            get
            {
                return _playtime;
            }
            set
            {
                SeekUDPtoPos = TimeSpan.FromSeconds(value);
                CancelForSeekUDP();
            }
        }
        public DateTime PlayDateTime { get => _playDateTime; }

        public void Stop()
        {
            SeekUDPtoPos = TimeSpan.FromDays(100);
            CancelForSeekUDP();
        }

        public int Play(Stream TSstream, string filename, CancellationToken ct)
        {
            DateTime InitialTOT = default(DateTime);
            long bytePerSec = 0;
            long? SkipByte = null;
            _duration = 0;
            _playtime = 0;
            _playDateTime = default(DateTime);

            while (true)
            {
                synchronizationContext.Post((o) =>
                {
                    PressKeyForOtherApp();
                }, null);

                var internalToken = seekUDP_ct_source.Token;
                var externalToken = ct;
                try
                {
                    using (CancellationTokenSource linkedCts =
                           CancellationTokenSource.CreateLinkedTokenSource(internalToken, externalToken))
                    {
                        using (var UDP = new UDP_TS_Stream(linkedCts.Token))
                        {
                            if (SeekUDPtoPos < TimeSpan.FromDays(30))
                            {
                                if (SendDuration != default(TimeSpan))
                                    UDP.SendDuration = SendDuration - SeekUDPtoPos;

                                if (InitialTOT != default(DateTime))
                                    UDP.SendStartTime = InitialTOT + SeekUDPtoPos;
                                else
                                    UDP.SendDelay = SeekUDPtoPos;
                            }
                            else
                            {
                                UDP.SendDuration = SendDuration;
                                if (SkipByte == null)
                                {
                                    UDP.SendDelay = SendStartDelay;
                                    UDP.SendStartTime = SendStartTime;
                                }
                                else
                                {
                                    if (SendStartTime != default(DateTime))
                                        UDP.SendStartTime = SendStartTime;
                                    else if (InitialTOT != default(DateTime))
                                        UDP.SendStartTime = InitialTOT + SendStartDelay;
                                }
                            }
                            UDP.TOTChangeHander += (src, evnt) =>
                            {
                                synchronizationContext.Post(
                                    (o) =>
                                    {
                                        if (linkedCts.Token.IsCancellationRequested) return;
                                        var eo = o as TOTChangeEventArgs;
                                        if (InitialTOT == default(DateTime))
                                        {
                                            InitialTOT = (eo.initialTOT == default(DateTime)) ? eo.TOT_JST : eo.initialTOT;
                                        }
                                        bytePerSec = eo.bytePerSec;
                                        _duration = (double)TSstream.Length / eo.bytePerSec;
                                        _playtime = (eo.TOT_JST - InitialTOT).TotalSeconds;
                                        _playDateTime = eo.TOT_JST;
                                    }, evnt);
                            };
                            SeekUDPtoPos = TimeSpan.FromDays(100);
                            bool bflag = false;
                            bool cflag = false;
                            TSstream.CopyToAsync(UDP, 2 * 1024 * 1024, linkedCts.Token)
                                .ContinueWith((t) =>
                                {
                                    if (t.IsCanceled)
                                    {
                                        bflag = true;
                                        return;
                                    }
                                    if (t.IsFaulted)
                                    {
                                        var e = t.Exception;
                                        e.Handle(x =>
                                        {
                                            if (x is PlayEOF_CanceledException)
                                            {
                                                bflag = true;
                                                return true;
                                            }
                                            if (x is SenderBreakCanceledException)
                                            {
                                                var ex = x as SenderBreakCanceledException;

                                                bytePerSec = ex.bytePerSec;

                                                if (SkipByte != null)
                                                    SkipByte += ex.WaitForByte;
                                                else
                                                    SkipByte = ex.WaitForByte;

                                                if (InitialTOT == default(DateTime))
                                                    InitialTOT = ex.InitialTOT;

                                                if (SkipByte > TSstream.Length)
                                                {
                                                    bflag = true;
                                                }
                                                else
                                                {
                                                    TSstream.Position = SkipByte.Value;
                                                    cflag = true;
                                                }
                                                return true;
                                            }
                                            return false;
                                        });
                                        return;
                                    }

                                }, linkedCts.Token)
                                .Wait(linkedCts.Token);

                            if (bflag)
                                break;
                            if (cflag)
                                continue;
                        }
                    }
                    break;
                }
                catch (OperationCanceledException)
                {
                    if (internalToken.IsCancellationRequested)
                    {
                        if (SeekUDPtoPos < TimeSpan.FromDays(30))
                        {
                            SkipByte = (long)(SeekUDPtoPos.TotalSeconds * bytePerSec * 0.95);
                            if (SkipByte > TSstream.Length)
                                SkipByte = TSstream.Length;

                            TSstream.Position = SkipByte.Value;
                            continue;
                        }
                        SeekUDPtoPos = TimeSpan.FromDays(100);
                        TSstream.ReadTimeout = 0;
                        Thread.Sleep(100);
                        break;
                    }
                    else if (externalToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    break;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return 0;
        }

        [DllImport("User32.dll")]
        public static extern int PostMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);

        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;

        private void PressKeyForOtherApp()
        {
            try
            {
                var mainWindowHandle = System.Diagnostics.Process.GetProcessesByName(ConfigTSsend.SendVK_Application)[0].MainWindowHandle;
                PostMessage(mainWindowHandle, WM_KEYDOWN, (int)ConfigTSsend.SendVK, 0);
                PostMessage(mainWindowHandle, WM_KEYUP, (int)ConfigTSsend.SendVK, 0);
            }
            catch { }
        }
    }
}
