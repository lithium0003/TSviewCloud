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
    public partial class FormConflicts : Form
    {
        public FormConflicts()
        {
            InitializeComponent();
            result = new List<ConflictResult>();
            bs = new BindingSource(result, string.Empty);
            dataGridView1.DataSource = bs;
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
            Hide();
        }

        public void Init()
        {
            synchronizationContext = SynchronizationContext.Current;
        }

        List<ConflictResult> result;
        BindingSource bs;

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
            synchronizationContext.Post((o) =>
            {
                result.Add(new ConflictResult(remotepath, reason));
                bs.ResetBindings(false);

                Show();
            }, null);
        }
    }
}
