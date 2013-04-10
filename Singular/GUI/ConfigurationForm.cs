
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


            InitializeHealContextDropdown(StyxWoW.Me.Class);
            chkUseInstanceBehaviorsWhenSolo.Checked = SingularRoutine.ForceInstanceBehaviors;

            if (!timer1.Enabled)
                timer1.Start();
        }

        /// <summary>
        /// populates the cboHealContext dropdown with an object list of all healing context setups
        /// that apply to current character.  will initially clear the list, then populate, and
        /// finally set the current context as selected (or first in list if not applicable)
        /// </summary>
        /// <param name="cls"></param>
        private void InitializeHealContextDropdown(WoWClass cls)
        {
            cboHealContext.Items.Clear();
            if (cls == WoWClass.Shaman)
            {
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Normal, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().Normal));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().Battleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().Instance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.ShamanRestoration, SingularSettings.Instance.Shaman().Raid));
            }

            if (cls == WoWClass.Priest)
            {
/*
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Normal, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscNormal));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.PriestDiscipline, SingularSettings.Instance.Priest().DiscRaid));
 */ 
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Battlegrounds, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyBattleground));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Instances, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyInstance));
                cboHealContext.Items.Add(new HealContextItem(HealingContext.Raids, WoWSpec.PriestHoly, SingularSettings.Instance.Priest().HolyRaid));
            }

            cboHealContext.Enabled = cboHealContext.Items.Count > 0;

            foreach (var obj in cboHealContext.Items)
            {
                HealContextItem ctx = (HealContextItem)obj;
                if (ctx.Spec == StyxWoW.Me.Specialization && ctx.Context == SingularRoutine.CurrentHealContext)
                {
                    cboHealContext.SelectedItem = ctx;
                    break;
                }
            }

            if (cboHealContext.SelectedItem == null && cboHealContext.Items.Count > 0)
                cboHealContext.SelectedIndex = 0;

        }

        private void Instance_OnTargetListUpdateFinished(object context)
        {
            if (InvokeRequired)
            {
                Invoke(new TargetListUpdateFinishedDelegate(Instance_OnTargetListUpdateFinished), context);
                return;
            }

            int i = 0;
            var sb = new StringBuilder();
            foreach (WoWUnit u in Targeting.Instance.TargetList)
            {
                sb.AppendLine(u.SafeName() + " - " + u.HealthPercent.ToString("F1") + "% - " + u.Distance.ToString("F1") + " yds");
                if (++i == 5)
                    break;
            }
            lblTargets.Text = sb.ToString();

            i = 0;
            sb = new StringBuilder();
            foreach (WoWUnit u in HealerManager.Instance.HealList)
            {
                sb.AppendLine(u.SafeName() + " - " + u.HealthPercent.ToString("F1") + "% - " + u.Distance.ToString("F1") + " yds");
                if (++i == 5)
                    break;
            }
            lblHealTargets.Text = sb.ToString();
        }

#pragma warning disable 168 // for ex below

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        { // prevent an exception from closing HB.
            try
            {
                SingularSettings.Instance.FormHeight = this.Height;
                SingularSettings.Instance.FormWidth = this.Width;
                SingularSettings.Instance.FormTabIndex = tabControl1.SelectedIndex; ;

                ((SingularSettings)pgGeneral.SelectedObject).Save();

                if (pgClass.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgClass.SelectedObject).Save();

                if (pgHotkeys.SelectedObject != null)
                    ((Styx.Helpers.Settings)pgHotkeys.SelectedObject).Save();

                foreach (var obj in cboHealContext.Items)
                {
                    HealContextItem ctx = (HealContextItem)obj;
                    ctx.Settings.Save();
                }

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
            int i = 0;
            var sb = new StringBuilder();

            foreach (WoWUnit u in Targeting.Instance.TargetList)
            {
                try
                {
                    sb.AppendLine(u.SafeName().AlignLeft(18) + " " + u.HealthPercent.ToString("F1").AlignRight(5) + "%  " + u.Distance.ToString("F1").AlignRight(5) + " yds");
                    if (++i == 5)
                        break;
                }
                catch (System.AccessViolationException)
                {
                }
                catch (Styx.InvalidObjectPointerException)
                {
                }
            }
            lblTargets.Text = sb.ToString();

            if (!HealerManager.NeedHealTargeting)
                return;

            i = 0;
            sb = new StringBuilder();
            foreach (WoWUnit u in HealerManager.Instance.HealList)
            {
                try
                {
                    sb.AppendLine(u.SafeName().AlignLeft(15) + "- " + u.HealthPercent.ToString("F1").AlignRight(5) + "% @ " + u.Distance.ToString("F1").AlignRight(5) + " yds");
                    if (++i == 5)
                        break;
                }
                catch (System.AccessViolationException)
                {
                }
                catch (Styx.InvalidObjectPointerException)
                {
                }
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
            HealContextItem ctx = (HealContextItem)cboHealContext.SelectedItem;
            pgHeal.SelectedObject = ctx.Settings;
        }

        private void chkUseInstanceBehaviorsWhenSolo_CheckedChanged(object sender, EventArgs e)
        {
            SingularRoutine.ForceInstanceBehaviors = chkUseInstanceBehaviorsWhenSolo.Checked;
        }

        private static int LogMarkIndex = 1;
        private void btnLogMark_Click(object sender, EventArgs e)
        {
            Logger.Write( Color.HotPink, " LOGMARK # {0} at {1}", LogMarkIndex++, DateTime.Now.ToString("HH:mm:ss.fff"));
            btnLogMark.Text = "LOGMARK! " + LogMarkIndex;
        }
    }

    public class HealContextItem
    {
        public HealingContext Context { get; set; }
        public WoWSpec Spec { get; set; }
        public HealerSettings Settings { get; set; }

        public override bool Equals(object obj)
        {
            return ToString() == obj.ToString();
        }

        public override string ToString()
        {
            return Spec.ToString().CamelToSpaced().Trim() + " - " + Context.ToString();
        }

        // ctor for list item
        public HealContextItem(HealingContext context, WoWSpec spec, HealerSettings stgs)
        {
            Context = context;
            Spec = spec;
            Settings = stgs;
        }

        // ctor for lookup only
        public HealContextItem(HealingContext context, WoWSpec spec)
        {
            Context = context;
            Spec = spec;
        }
    }

}