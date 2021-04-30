using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CheapLoc.Test
{
    public partial class TestForm : Form
    {
        public TestForm()
        {
            InitializeComponent();

            Loc.Setup(File.ReadAllText("de_DE.json"));

            label1.Text = Loc.Localize("LabelTest", "This is a label test.");
            button1.Text = Loc.Localize("ButtonTest", "Click me!");
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Loc.Localize("MsgBoxText", "A box! Nice!"));
        }
    }
}
