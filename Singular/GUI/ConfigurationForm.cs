
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Singular.GUI
{
    public partial class ConfigurationForm : Form
    {
        public ConfigurationForm()
        {
            InitializeComponent();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == DialogResult.OK || DialogResult == DialogResult.Yes)
            {
                Logger.WriteDebug(Color.LightGreen, "Settings saved, rebuilding behaviors...");
                HotkeyDirector.Update();
                MovementManager.Update();
                SingularRoutine.Instance.RebuildBehaviors();
                SingularSettings.Instance.LogSettings();
            }
            base.OnClosing(e);
        }

        private void ConfigurationForm_Load(object sender, EventArgs e)
        {
            // lblVersion.Text = string.Format("Version {0}", Assembly.GetExecutingAssembly().GetName().Version);
            lblVersion.Text = string.Format("Version {0}", SingularRoutine.GetSingularVersion());

            //HealTargeting.Instance.OnTargetListUpdateFinished += new Styx.Logic.TargetListUpdateFinishedDelegate(Instance_OnTargetListUpdateFinished);
            pgGeneral.SelectedObject = SingularSettings.Instance;

            Styx.Helpers.Settings toSelect = null;
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Warrior:
                    toSelect = SingularSettings.Instance.Warrior();
                    break;
                case WoWClass.Paladin:
                    toSelect = SingularSettings.Instance.Paladin();
                    break;
                case WoWClass.Hunter:
                    toSelect = SingularSettings.Instance.Hunter();
                    break;
                case WoWClass.Rogue:
                    toSelect = SingularSettings.Instance.Rogue();
                    break;
                case WoWClass.Priest:
                    toSelect = SingularSettings.Instance.Priest();
                    break;
                case WoWClass.DeathKnight:
                    toSelect = SingularSettings.Instance.DeathKnight();
                    break;
                case WoWClass.Shaman:
                    toSelect = SingularSettings.Instance.Shaman();
                    pgHealBattleground.SelectedObject = SingularSettings.Instance.Shaman().Battleground;
                    pgHealInstance.SelectedObject = SingularSettings.Instance.Shaman().Instance;
                    pgHealNormal.SelectedObject = SingularSettings.Instance.Shaman().Normal;
                    pgHealRaid.SelectedObject = SingularSettings.Instance.Shaman().Raid;
                    break;
                case WoWClass.Mage:
                    toSelect = SingularSettings.Instance.Mage();
                    break;
                case WoWClass.Warlock:
                    toSelect = SingularSettings.Instance.Warlock();
                    break;
                case WoWClass.Druid:
                    toSelect = SingularSettings.Instance.Druid();
                    break;
                default:
                    break;
            }
            if (toSelect != null)
            {
                pgClass.SelectedObject = toSelect;
            }

            pgHotkeys.SelectedObject = SingularSettings.Instance.Hotkeys();

            InitializeContextDropdown();

            chkUseInstanceBehaviorsWhenSolo.Checked = SingularRoutine.ForceInstanceBehaviors;

            if (!timer1.Enabled)
                timer1.Start();
        }

        private void InitializeContextDropdown()
        {
            if (pgHealBattleground.SelectedObject != null)
                cboHealContext.Items.Add(HealingContext.Battlegrounds);
            if (pgHealInstance.SelectedObject != null)
                cboHealContext.Items.Add(HealingContext.Instances);
            if (pgHealInstance.SelectedObject != null)
                cboHealContext.Items.Add(HealingContext.Raids);
            if (pgHealNormal.SelectedObject != null)
                cboHealContext.Items.Add(HealingContext.Normal);

            cboHealContext.Enabled = cboHealContext.Items.Count > 0;

            try
            {
                cboHealContext.SelectedItem = Singular.SingularRoutine.CurrentHealContext;
            }
            catch
            {
                if ( cboHealContext.Enabled)
                    cboHealContext.SelectedIndex = 0;
            }
        }

        private void Instance_OnTargetListUpdateFinished(object context)
        {
            if (InvokeRequired)
            {
                Invoke(new TargetListUpdateFinishedDelegate(Instance_OnTargetListUpdateFinished), context);
                return;
            }

            var sb = new StringBuilder();
            foreach (WoWPlayer u in HealerManager.Instance.HealList)
            {
                sb.AppendLine(u.Name + " - " + u.HealthPercent);
            }
            lblHealTargets.Text = sb.ToString();
        }

#pragma warning disable 168 // for ex below

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        { // prevent an exception from closing HB.
            try
            {
                ((SingularSettings)pgGeneral.SelectedObject).Save();

                if (pgClass.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgClass.SelectedObject).Save();

                if (pgHealBattleground.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHealBattleground.SelectedObject).Save();

                if (pgHealInstance.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHealInstance.SelectedObject).Save();

                if (pgHealRaid.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHealRaid.SelectedObject).Save();

                if (pgHealNormal.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHealNormal.SelectedObject).Save();

                ((Styx.Helpers.Settings)pgHotkeys.SelectedObject).Save();
                Close();
            }
            catch (Exception ex)
            {
                Logger.Write("ERROR saving settings: {0}", e);
            }
        }

#pragma warning disable 168

        private void timer1_Tick(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            foreach (WoWPlayer u in HealerManager.Instance.HealList.Where(p => p != null && p.IsValid))
            {
                sb.AppendLine(u.Name + " - " + u.HealthPercent);
            }
            lblHealTargets.Text = sb.ToString();
        }

        // private int lastTried = 0;

        private void button1_Click(object sender, EventArgs e)
        {
            ObjectManager.Update();
            SpellManager.CanCast("Evasion");
            Logger.Write("Current target is immune to frost? {0}", StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost));
            //var val = Enum.GetValues(typeof(WoWMovement.ClickToMoveType)).GetValue(lastTried++);
            //WoWMovement.ClickToMove(StyxWoW.Me.CurrentTargetGuid, (WoWMovement.ClickToMoveType)val);
            //Logging.Write("Trying " + val);
            //TotemManager.RecallTotems();
        }

        private void ConfigurationForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();
        }

        private void ShowPlayerNames_CheckedChanged(object sender, EventArgs e)
        {
            Extensions.ShowPlayerNames = ShowPlayerNames.Checked;
        }

        private void cboHealContext_SelectedIndexChanged(object sender, EventArgs e)
        {
            HealingContext ctx = (HealingContext)cboHealContext.SelectedItem;
            bool isBG = ctx == HealingContext.Battlegrounds;
            bool isInst = ctx == HealingContext.Instances;
            bool isNorm = ctx == HealingContext.Normal;
            bool isRaid = ctx == HealingContext.Raids;

            pgHealBattleground.Enabled = isBG;
            pgHealBattleground.Visible = isBG;

            pgHealInstance.Enabled = isInst;
            pgHealInstance.Visible = isInst;

            pgHealRaid.Enabled = isRaid;
            pgHealRaid.Visible = isRaid;

            pgHealNormal.Enabled = isNorm;
            pgHealNormal.Visible = isNorm;
        }

        private void chkUseInstanceBehaviorsWhenSolo_CheckedChanged(object sender, EventArgs e)
        {
            SingularRoutine.ForceInstanceBehaviors = chkUseInstanceBehaviorsWhenSolo.Checked;
        }
    }
}