using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloud
{
    public partial class FormLog : Form
    {
        private SynchronizationContext synchronizationContext;

        private MemoryStream backlog = null;
        private DateTime lastLogDate;
        private DateTime backlogDate;

        private TextWriter LogStream;
        public bool LogToFile
        {
            get { return LogStream != null; }
            set
            {
                if (value)
                {
                    if (LogStream == null)
                    {
                        try
                        {
                            LogStream = TextWriter.Synchronized(new StreamWriter(Stream.Synchronized(new FileStream(Path.Combine(TSviewCloudConfig.Config.LogPath, Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".log"), FileMode.Append, FileAccess.Write, FileShare.Read))));
                        }
                        catch { }
                    }
                }
                else
                {
                    if (LogStream != null)
                    {
                        LogStream.Flush();
                        LogStream = null;
                    }
                }
            }
        }

        public FormLog()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
        }
 
        private void FormLog_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogStream?.Flush();
            if (TSviewCloudConfig.Config.ApplicationExit) return;

            e.Cancel = true;
            Hide();
        }

        public void LogOut(string str)
        {
            str = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffff ") + str;

            LogStream?.Write(str+"\r\n");
            if (lastLogDate.AddSeconds(10) < DateTime.Now && backlog == null)
            {
                LogStream?.Flush();
                lastLogDate = DateTime.Now;

                synchronizationContext.Post((o) =>
                {
                    textBox_log.AppendText(o as string + "\r\n");
                }, str);
            }
            else
            {
                synchronizationContext.Post((o) =>
                {
                    timer1.Enabled = true;
                }, null);
                while (true)
                {
                    if (Interlocked.CompareExchange(ref backlog, new MemoryStream(), null) == null)
                    {
                        backlogDate = DateTime.Now;
                    }

                    MemoryStream tmplog = backlog;
                    if (tmplog == null) continue;

                    lock (tmplog)
                    {
                        if (tmplog.CanWrite)
                        {
                            var data = Encoding.UTF8.GetBytes(str + "\r\n");
                            tmplog.Write(data, 0, data.Length);
                            lastLogDate = DateTime.Now;
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            LogFinalize();
        }

        public void LogFinalize()
        {
            if (backlogDate.AddSeconds(5) < DateTime.Now && backlog != null)
            {
                LogStream?.Flush();

                MemoryStream oldms = backlog;
                if ((oldms = Interlocked.CompareExchange(ref backlog, null, oldms)) != null)
                {
                    lock (oldms)
                    {
                        oldms.Position = 0;
                        synchronizationContext.Post((o) =>
                        {
                            textBox_log.AppendText(o as string);
                        }, Encoding.UTF8.GetString(oldms.ToArray()));
                        oldms.Dispose();
                    }
                }
                synchronizationContext.Post((o) =>
                {
                    timer1.Enabled = backlog != null;
                }, null);
            }
        }

        public void LogOut(string format, params object[] args)
        {
            LogOut(string.Format(format, args));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            LogFinalize();
        }
    }

    class LogWindowStream : Stream
    {
        FormLog log;
        string strbuf = "";

        public LogWindowStream(FormLog window)
        {
            log = window;
        }
        public override long Length { get { return -1; } }
        public override bool CanRead { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override long Position
        {
            get { return -1; }
            set { }
        }
        public override void SetLength(long value) { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return -1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            string buf1 = Encoding.UTF8.GetString(buffer, offset, count);
            if (buf1.Contains("\r\n") || buf1.Contains("\r") || buf1.Contains("\n"))
            {
                foreach (var line in buf1.Split(new char[] { '\r', '\n' }))
                {
                    if (line == "")
                    {
                        LogOutput();
                    }
                    else
                    {
                        strbuf += line;
                    }
                }
            }
            else
            {
                strbuf += buf1;
            }
        }

        public override void Flush() { LogOutput(); }

        private void LogOutput()
        {
            if (strbuf == "") return;
            log.LogOut("[FFmpeg]" + strbuf);
            strbuf = "";
        }
    }
}
