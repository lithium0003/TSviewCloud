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

        public FormLog()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
        }
 
        private void FormLog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TSviewCloudConfig.Config.ApplicationExit) return;

            e.Cancel = true;
            Hide();
        }

        public void LogOut(string str)
        {
            if(lastLogDate.AddSeconds(5) < DateTime.Now && backlog == null)
            {
                lastLogDate = DateTime.Now;

                synchronizationContext.Post((o) =>
                {
                    textBox_log.AppendText(o as string + "\r\n");
                }, str);
                timer1.Enabled = false;
            }
            else
            {
                timer1.Enabled = true;
                while (true)
                {
                    if (Interlocked.CompareExchange(ref backlog, new MemoryStream(), null) == null)
                    {
                        backlogDate = DateTime.Now;
                    }

                    MemoryStream tmplog;
                    lock (tmplog = backlog)
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
            if (backlogDate.AddSeconds(1) < DateTime.Now && backlog != null)
            {
                MemoryStream oldms;
                lock (oldms = Interlocked.CompareExchange(ref backlog, null, backlog))
                {
                    oldms.Position = 0;
                    synchronizationContext.Post((o) =>
                    {
                        textBox_log.AppendText(o as string);
                    }, Encoding.UTF8.GetString(oldms.ToArray()));
                    oldms.Dispose();
                }
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
