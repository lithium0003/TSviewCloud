using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSviewCloudConfig;

namespace TSviewCloud
{
    public partial class FormMasterPass : Form
    {
        public FormMasterPass()
        {
            InitializeComponent();
            textBox1.Text = Config.MasterPasswordRaw;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox1.PasswordChar = (checkBox1.Checked) ? '*' : '\0';
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Config.MasterPassword = textBox1.Text;
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                button1.PerformClick();
            }
        }
    }
}
