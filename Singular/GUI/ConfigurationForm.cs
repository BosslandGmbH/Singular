using System;
using System.Windows.Forms;

using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;

namespace Singular.GUI
{
    public partial class ConfigurationForm : Form
    {
        public ConfigurationForm()
        {
            InitializeComponent();
        }

        private void ConfigurationForm_Load(object sender, EventArgs e)
        {
            pgGeneral.SelectedObject = SingularSettings.Instance;
            SingularSettings main = SingularSettings.Instance;
            Styx.Helpers.Settings toSelect = null;
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warrior:
                    toSelect = main.Warrior;
                    break;
                case WoWClass.Paladin:
                    toSelect = main.Paladin;
                    break;
                case WoWClass.Hunter:
                    toSelect = main.Hunter;
                    break;
                case WoWClass.Rogue:
                    toSelect = main.Rogue;
                    break;
                case WoWClass.Priest:
                    toSelect = main.Priest;
                    break;
                case WoWClass.DeathKnight:
                    toSelect = main.DeathKnight;
                    break;
                case WoWClass.Shaman:
                    toSelect = main.Shaman;
                    break;
                case WoWClass.Mage:
                    toSelect = main.Mage;
                    break;
                case WoWClass.Warlock:
                    toSelect = main.Warlock;
                    break;
                case WoWClass.Druid:
                    toSelect = main.Druid;
                    break;
                default:
                    break;
            }
            if (toSelect != null)
            {
                pgClass.SelectedObject = toSelect;
            }
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            ((Styx.Helpers.Settings)pgGeneral.SelectedObject).Save();
            if (pgClass.SelectedObject != null)
            {
                ((Styx.Helpers.Settings)pgClass.SelectedObject).Save();
            }
            Close();
        }
    }
}