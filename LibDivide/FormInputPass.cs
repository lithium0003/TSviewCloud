using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibDivide
{
    public partial class FormInputPass : Form
    {
        public string Password
        {
            get { return textBox1.Text; }
        }
        public string CryptNameHeader
        {
            get { return comboBox1.SelectedItem as string; }
        }

        public FormInputPass()
        {
            InitializeComponent();
        }

        private void FormInputPass_Load(object sender, EventArgs e)
        {
            comboBox1.Items.AddRange(TSviewCloudConfig.ConfigCarotDAV.CarotDAV_crypt_names);
            comboBox1.SelectedItem = TSviewCloudConfig.ConfigCarotDAV.CryptNameHeader;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox1.PasswordChar = (checkBox1.Checked) ? '*' : '\0';
        }
    }
}
