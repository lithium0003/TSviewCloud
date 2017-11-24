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
    public partial class FormInputAttribute : Form
    {
        public FormInputAttribute()
        {
            InitializeComponent();
        }

        public DateTime ModifiedTime
        {
            get => dateTimePicker1.Value;
            set => dateTimePicker1.Value = value;
        }

        public DateTime CreatedTime
        {
            get => dateTimePicker2.Value;
            set => dateTimePicker2.Value = value;
        }
    }
}
