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
    public partial class FormErrorLog : Form
    {
        public FormErrorLog()
        {
            InitializeComponent();
        }

        private static readonly SynchronizationContext synchronizationContext = SynchronizationContext.Current;
        private static readonly FormErrorLog _instance = new FormErrorLog();

        public static FormErrorLog Instance
        {
            get
            {
                return _instance;
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
            base.OnShown(e);
            if (Program.MainForm != null)
            {
                Point p = new Point(Program.MainForm.Left + Program.MainForm.Width, Program.MainForm.Bottom - Height);
                Location = p;
            }
        }

        public void ErrorLog(string str)
        {
            synchronizationContext.Post((o) =>
            {
                textBox1.AppendText(o as string);
            }, str);
        }

        public void ErrorLog(string format, params object[] args)
        {
            ErrorLog(string.Format(format, args));
        }
    }
}
