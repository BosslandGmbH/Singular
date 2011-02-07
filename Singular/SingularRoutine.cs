using System.Drawing;
using System.Reflection;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        protected Composite _combatBehavior;
        protected Composite _combatBuffsBehavior;
        protected Composite _healBehavior;
        protected Composite _preCombatBuffsBehavior;
        protected Composite _pullBehavior;
        protected Composite _pullBuffsBehavior;
        protected Composite _restBehavior;

        public override string Name { get { return "Singular"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public LocalPlayer Me { get { return StyxWoW.Me; } }

        public WoWContext CurrentWoWContext
        {
            get
            {
                if (Battlegrounds.IsInsideBattleground)
                {
                    return WoWContext.Battlegrounds;
                }
                if (StyxWoW.Me.IsInInstance)
                {
                    return WoWContext.Instances;
                }
                return WoWContext.Normal;
            }
        }

        public string LastSpellCast { get; set; }

        public override Composite CombatBehavior { get { return _combatBehavior; } }

        public override Composite CombatBuffBehavior { get { return _combatBuffsBehavior; } }

        public override Composite HealBehavior { get { return _healBehavior; } }

        public override Composite PreCombatBuffBehavior { get { return _preCombatBuffsBehavior; } }

        public override Composite PullBehavior { get { return _pullBehavior; } }

        public override Composite PullBuffBehavior { get { return _pullBuffsBehavior; } }

        public override Composite RestBehavior { get { return _restBehavior; } }

        public void Log(string message)
        {
            Logging.Write(Color.Orange, message);
        }

        public override void Initialize()
        {
            Logger.Write("Starting Singular v" + Assembly.GetExecutingAssembly().GetName().Version);
            AttachEventHandlers();
            Logger.Write("Determining talent spec.");
            TalentManager.Update();
            Logger.Write("Current spec is " + TalentManager.CurrentSpec.ToString().CamelToSpaced());

            CreateBehaviors();
        }

        public void CreateBehaviors()
        {
            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite(true, BehaviorType.Combat, out _combatBehavior))
            {
                return;
            }
            if (!EnsureComposite(true, BehaviorType.Pull, out _pullBehavior))
            {
                return;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            if (!EnsureComposite(false, BehaviorType.Rest, out _restBehavior))
            {
                _restBehavior = CreateDefaultRestComposite();
            }

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs, out _combatBuffsBehavior);
            EnsureComposite(false, BehaviorType.Heal, out _healBehavior);
            EnsureComposite(false, BehaviorType.PullBuffs, out _pullBuffsBehavior);
            EnsureComposite(false, BehaviorType.PreCombatBuffs, out _preCombatBuffsBehavior);
        }

        private bool EnsureComposite(bool error, BehaviorType type, out Composite composite)
        {
            Logging.Write("Creating " + type + " behavior.");
            composite = CompositeBuilder.GetComposite(this, Class, TalentManager.CurrentSpec, type, CurrentWoWContext);
            if (composite == null && error)
            {
                StopBot(
                    "Singularity currently does not support " + type + " for this class/spec combination, in this context! [" + Class + ", " +
                    TalentManager.CurrentSpec + ", " + CurrentWoWContext + "]");
                return false;
            }
            return true;
        }

        private static void StopBot(string reason)
        {
            Logger.Write(reason);
            TreeRoot.Stop();
        }
    }
}