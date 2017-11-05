using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloud
{
    public partial class FormNewServer : Form
    {
        public FormNewServer()
        {
            InitializeComponent();
        }

        private TSviewCloudPlugin.IRemoteServer _target;
        private string _servername;

        public string ServerName {
            get
            {
                return _servername;
            }
            set {
                _servername = value;
                textBox_Name.Text = TSviewCloudPlugin.RemoteServerFactory.ServerFixedName(_servername);
            }
        }
        public TSviewCloudPlugin.IRemoteServer Target { get { return _target; } }

        private void button1_Click(object sender, EventArgs e)
        {
            _target = TSviewCloudPlugin.RemoteServerFactory.Get(ServerName, textBox_Name.Text);
            if (!_target.Add())
            {
                DialogResult = DialogResult.Abort;
            }
        }
    }
}
