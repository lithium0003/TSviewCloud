using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibCryptRclone
{
    public partial class FormInputPass : Form
    {
        public string Password
        {
            get { return textBox1.Text; }
        }
        public string Salt
        {
            get { return textBox2.Text; }
        }
        public bool FilenameEncryption
        {
            get { return radioButton_Standard.Checked; }
        }

        public FormInputPass()
        {
            InitializeComponent();
        }

 
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox1.PasswordChar = (checkBox1.Checked) ? '*' : '\0';
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            textBox2.PasswordChar = (checkBox2.Checked) ? '*' : '\0';
        }
    }
}
