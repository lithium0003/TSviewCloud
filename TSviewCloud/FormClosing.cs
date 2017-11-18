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
    public partial class FormClosing : Form
    {
        public FormClosing()
        {
            InitializeComponent();
        }

        private static FormClosing _instance;

        public static FormClosing Instance
        {
            get
            {
                if (_instance == null || _instance.IsDisposed)
                {
                    _instance = new FormClosing();
                }
                return _instance;
            }
        }

        public bool Active
        {
            get { return TopMost; }
            set { TopMost = value; }
        }

        private void FormClosing_Load(object sender, EventArgs e)
        {
            if (Program.MainForm != null)
            {
                Point p = new Point(Program.MainForm.Left + Program.MainForm.Width / 2 - Width /2 , Program.MainForm.Top + Program.MainForm.Height / 2 - Height / 2);
                Location = p;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BringToFront();
        }

        int showcount = 0;
        public void IncShowCount()
        {
            Interlocked.Increment(ref showcount);
            Show();
        }

        public void DecShowCount()
        {
            if (Interlocked.Decrement(ref showcount) > 0) return;
            Close();
        }

     }
}
