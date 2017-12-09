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
    public partial class FormInput : Form
    {
        public string Divider
        {
            get { return comboBox1.Text; }
        }

        public FormInput()
        {
            InitializeComponent();
        }

        private void FormInput_Load(object sender, EventArgs e)
        {
            comboBox1.Items.AddRange(TSviewCloudConfig.ConfigCarotDAV.CarotDAV_crypt_names);
            comboBox1.SelectedItem = TSviewCloudConfig.ConfigCarotDAV.CryptNameHeader;
        }

        private void comboBox1_TextUpdate(object sender, EventArgs e)
        {
            if (comboBox1.Text == "") comboBox1.SelectedIndex = 0;
            textBox1.Text = "divided_name\n" + string.Join("\n", Enumerable.Range(1, 5).Select(i => string.Format("{0}{1}{2}", "divided_name", Divider, i)));
        }
    }
}
