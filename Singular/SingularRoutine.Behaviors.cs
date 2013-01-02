using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Singular.ClassSpecific;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common;

namespace Singular
{
    partial class SingularRoutine
    {
        private Composite _combatBehavior;
        private Composite _combatBuffsBehavior;
        private Composite _healBehavior;
        private Composite _preCombatBuffsBehavior;
        private Composite _pullBehavior;
        private Composite _pullBuffsBehavior;
        private Composite _restBehavior;
        public override Composite CombatBehavior { get { return _combatBehavior; } }
        public override Composite CombatBuffBehavior { get { return _combatBuffsBehavior; } }
        public override Composite HealBehavior { get { return _healBehavior; } }
        public override Composite PreCombatBuffBehavior { get { return _preCombatBuffsBehavior; } }
        public override Composite PullBehavior { get { return _pullBehavior; } }
        public override Composite PullBuffBehavior { get { return _pullBuffsBehavior; } }
        public override Composite RestBehavior { get { return _restBehavior; } }

        public bool RebuildBehaviors(bool silent = false)
        {
            Logger.PrintStackTrace("RebuildBehaviors called.");

            InitBehaviors();

            // DO NOT UPDATE: This will cause a recursive event
            // Update the current context. Handled in SingularRoutine.Context.cs
            //UpdateContext();

            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite(true, BehaviorType.Combat))
            {
                return false;
            }

            if (!EnsureComposite(true, BehaviorType.Pull))
            {
                return false;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            EnsureComposite(false, BehaviorType.Rest);
            if ( TreeHooks.Instance.Hooks[BehaviorType.Rest.ToString()] == null)
                TreeHooks.Instance.ReplaceHook( BehaviorType.Rest.ToString(), Helpers.Rest.CreateDefaultRestBehaviour());


            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs);
            EnsureComposite(false, BehaviorType.Heal);
            EnsureComposite(false, BehaviorType.PullBuffs);
            EnsureComposite(false, BehaviorType.PreCombatBuffs);

#if SHOW_BEHAVIOR_LOAD_DESCRIPTION
            // display concise single line describing what behaviors we are loading
            if (!silent)
            {
                string sMsg = "";
                if (_healBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Heal";
                if (_pullBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PullBuffs";
                if (_pullBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Pull";
                if (_preCombatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PreCombatBuffs";
                if (_combatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " CombatBuffs";
                if (_combatBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Combat";
                if (_restBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Rest";

                Logger.Write(Color.LightGreen, "Loaded{0} behaviors for {1}: {2}", Me.Specialization.ToString().CamelToSpaced(), SingularRoutine.CurrentWoWContext.ToString(), sMsg);
            }
#endif
            return true;
        }

        /// <summary>
        /// initialize all base behaviors.  replaceable portion which will vary by context is represented by a single
        /// HookExecutor that gets assigned elsewhere (typically EnsureComposite())
        /// </summary>
        private void InitBehaviors()
        {
            if (_restBehavior != null)
                return;

            _restBehavior = new Decorator(
                ret => AllowBehaviorUsage() && !SingularSettings.Instance.DisableNonCombatBehaviors,
                new LockSelector(new HookExecutor(BehaviorType.Rest.ToString()))
                );

            _preCombatBuffsBehavior = new Decorator(
                ret => AllowBehaviorUsage() && !SingularSettings.Instance.DisableNonCombatBehaviors,
                new LockSelector(
                    Item.CreateUseAlchemyBuffsBehavior(),
                    // Generic.CreateFlasksBehaviour(),
                    new HookExecutor(BehaviorType.PreCombatBuffs.ToString())
                    )
                );

            _pullBuffsBehavior = new LockSelector(new HookExecutor(BehaviorType.PullBuffs.ToString()));

            _combatBuffsBehavior = new Decorator(
                ret => AllowBehaviorUsage(),
                new LockSelector(
                    new Decorator(ret => !HotkeyManager.IsCombatEnabled, new ActionAlwaysSucceed()),
                    Generic.CreateUseTrinketsBehaviour(),
                    Generic.CreatePotionAndHealthstoneBehavior(),
                    Generic.CreateRacialBehaviour(),
                    new HookExecutor( BehaviorType.CombatBuffs.ToString())
                    )
                );

            _healBehavior = new LockSelector(new HookExecutor(BehaviorType.Heal.ToString()));

            _pullBehavior = new LockSelector(new HookExecutor(BehaviorType.Pull.ToString()));

            _combatBehavior = new LockSelector(
                new Decorator(
                    ret => !HotkeyManager.IsCombatEnabled,
                    new ActionAlwaysSucceed()
                    ),
                new HookExecutor(BehaviorType.Combat.ToString())
                );
        }

        private static bool AllowBehaviorUsage()
        {
            return !IsMounted && (!Me.IsOnTransport || Me.Transport.Entry == 56171);
        }

        /// <summary>
        /// Ensures we have a composite for the given BehaviorType.  
        /// </summary>
        /// <param name="error">true: report error if composite not found, false: allow null composite</param>
        /// <param name="type">BehaviorType that should be loaded</param>
        /// <returns>true: composite loaded and saved to hook, false: failure</returns>
        private bool EnsureComposite(bool error, BehaviorType type)
        {
            int count = 0;
            Composite composite;

            Logger.WriteDebug("Creating " + type + " behavior.");

            composite = CompositeBuilder.GetComposite(Class, TalentManager.CurrentSpec, type, CurrentWoWContext, out count);

            // handle those composites we need to default if not found
            if (composite == null)
            {
                switch (type)
                {
                case BehaviorType.Rest:
                    composite = Helpers.Rest.CreateDefaultRestBehaviour();
                    break;
                }
            }

            TreeHooks.Instance.ReplaceHook(type.ToString(), composite);

            if ((composite == null || count <= 0) && error)
            {
                StopBot(
                    string.Format(
                        "Singular currently does not support {0} for this class/spec combination, in this context! [{1}, {2}, {3}]",
                        type, StyxWoW.Me.Class, TalentManager.CurrentSpec, CurrentWoWContext));
                return false;
            }

            return composite != null;
        }

        #region Nested type: LockSelector

        /// <summary>
        /// This behavior wraps the child behaviors in a 'FrameLock' which can provide a big performance improvement 
        /// if the child behaviors makes multiple api calls that internally run off a frame in WoW in one CC pulse.
        /// </summary>
        private class LockSelector : PrioritySelector
        {
            public LockSelector(params Composite[] children) : base(children)
            {
            }

            public override RunStatus Tick(object context)
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    return base.Tick(context);
                }
            }
        }

        #endregion
    }
}