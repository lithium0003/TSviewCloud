using System;
using System.Collections.Concurrent;
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
    public partial class FormConflicts : Form
    {
        public FormConflicts()
        {
            InitializeComponent();
            result = new List<ConflictResult>();
        }

        private static SynchronizationContext synchronizationContext;
        private static FormConflicts _instance;

        public static FormConflicts Instance
        {
            get
            {
                return _instance?? (_instance = new FormConflicts());
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (TSviewCloudConfig.Config.ApplicationExit) return;
            e.Cancel = true;
            result.Clear();
            listView1.VirtualListSize = 0;
            Hide();
        }

        public void Init()
        {
            synchronizationContext = SynchronizationContext.Current;
        }

        List<ConflictResult> result;
 
        public class ConflictResult
        {
            string remotepath;
            string reason;

            public ConflictResult(string remotepath, string reason)
            {
                Remotepath = remotepath ?? throw new ArgumentNullException(nameof(remotepath));
                Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            }

            public string Remotepath { get => remotepath; set => remotepath = value; }
            public string Reason { get => reason; set => reason = value; }
        }

        public void AddResult(string remotepath, string reason)
        {
            lock (result)
            {
                result.Add(new ConflictResult(remotepath, reason));
            }

            synchronizationContext.Post((o) =>
            {
                System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
                listView1.VirtualListSize = result.Count;

                if (!Visible)
                {
                    timer1.Enabled = false;
                    timer1.Enabled = true;
                }
            }, null);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            Show();
        }

        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex > result.Count)
                e.Item = new ListViewItem(new string[6]);
            else
            {
                e.Item = new ListViewItem(new string[] { result[e.ItemIndex].Remotepath, result[e.ItemIndex].Reason });
                if (result[e.ItemIndex].Reason.Contains("error"))
                    e.Item.BackColor = Color.PaleVioletRed;
            }
        }
    }
}
