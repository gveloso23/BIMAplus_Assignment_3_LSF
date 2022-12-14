using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

namespace LSFRevit
{
    public partial class Form1 : Form
    {
        List<FamilySymbolAbs> ProfilesExterior = new List<FamilySymbolAbs>();
        List<FamilySymbolAbs> ProfilesInterior = new List<FamilySymbolAbs>();
        public FamilySymbol ExteriorFrame { get; private set; }
        public FamilySymbol InteriorFrame { get; private set; }
        public int SpacingFrame { get; private set; }

        public Form1(List<FamilySymbol> profiles)
        {
            InitializeComponent();

            foreach (var item in profiles)
            {
                ProfilesExterior.Add(new FamilySymbolAbs(item));
                ProfilesInterior.Add(new FamilySymbolAbs(item));
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.DataSource = ProfilesExterior;
            comboBox1.DisplayMember = "FamilySymbolName";
            comboBox1.ValueMember = "FamilySymbol";

            comboBox2.DataSource = ProfilesInterior;
            comboBox2.DisplayMember = "FamilySymbolName";
            comboBox2.ValueMember = "FamilySymbol";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ExteriorFrame = (comboBox1.SelectedItem as FamilySymbolAbs).FamilySymbol;
            InteriorFrame = (comboBox2.SelectedItem as FamilySymbolAbs).FamilySymbol;
            SpacingFrame = trackBar1.Value;

            DialogResult = DialogResult.OK;

            Form1.ActiveForm.Close();
        }

    }
    public class FamilySymbolAbs
    {
        public FamilySymbol FamilySymbol { get; set; }
        public string FamilySymbolName { get; set; }

        public FamilySymbolAbs(FamilySymbol familySymbol)
        {
            FamilySymbol = familySymbol;
            FamilySymbolName = familySymbol.Name;
        }
    }
}
