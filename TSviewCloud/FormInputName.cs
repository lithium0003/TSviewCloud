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
    public partial class FormInputName : Form
    {
        public FormInputName()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Point p = new Point(Program.MainForm.Left + Program.MainForm.Width / 2 - Width / 2, Program.MainForm.Top + Program.MainForm.Height / 2 - Height / 2);
            Location = p;
        }

        public string NewItemName { get { return textBox1.Text; } set { textBox1.Text = value; } }
    }
}
