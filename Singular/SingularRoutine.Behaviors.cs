using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Singular.ClassSpecific;
using System.Drawing;

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

            // DO NOT UPDATE: This will cause a recursive event
            // Update the current context. Handled in SingularRoutine.Context.cs
            //UpdateContext();

            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite(true, BehaviorType.Combat, out _combatBehavior))
            {
                return false;
            }

            if (!EnsureComposite(true, BehaviorType.Pull, out _pullBehavior))
            {
                return false;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            if (!EnsureComposite(false, BehaviorType.Rest, out _restBehavior))
            {
                if ( !silent)
                    Logger.WriteDebug("Using default rest behavior.");
                _restBehavior = Helpers.Rest.CreateDefaultRestBehaviour();
            }

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs, out _combatBuffsBehavior);
            EnsureComposite(false, BehaviorType.Heal, out _healBehavior);
            EnsureComposite(false, BehaviorType.PullBuffs, out _pullBuffsBehavior);
            EnsureComposite(false, BehaviorType.PreCombatBuffs, out _preCombatBuffsBehavior);

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

            // Since we can be lazy, we're going to fix a bug right here and now.
            // We should *never* cast buffs while mounted. EVER. So we simply wrap it in a decorator, and be done with it.
            // 4/11/2012 - Changed to use a LockSelector to increased performance.
            _preCombatBuffsBehavior =
                new Decorator(
                    ret => !IsMounted && !Me.IsOnTransport && !SingularSettings.Instance.DisableNonCombatBehaviors,
                    new LockSelector(
                        Item.CreateUseAlchemyBuffsBehavior(),
                        // Generic.CreateFlasksBehaviour(),
                        _preCombatBuffsBehavior ?? new PrioritySelector()
                        ));

            _combatBuffsBehavior = new Decorator(ret => !IsMounted && !Me.IsOnTransport,
                new LockSelector( 
                    Generic.CreateUseTrinketsBehaviour(),
                    Generic.CreatePotionAndHealthstoneBehavior(),
                    Generic.CreateRacialBehaviour(),
                    _combatBuffsBehavior ?? new PrioritySelector()));

            // There are some classes that uses spells in rest behavior. Basicly we don't want Rest to be called while flying.
            _restBehavior =
                new Decorator(
                    ret => !Me.IsFlying && !Me.IsOnTransport && !SingularSettings.Instance.DisableNonCombatBehaviors,
                    new LockSelector(_restBehavior ?? new PrioritySelector())
                    );

            // Wrap all the behaviors with a LockSelector which basically wraps the child bahaviors with a framelock.
            // This will generally reduce the time it takes to pulse the behavior thus increasing performance of the cc
            if (_healBehavior != null)
            {
                _healBehavior = new LockSelector(_healBehavior);
            }

            if (_pullBuffsBehavior != null)
            {
                _pullBuffsBehavior = new LockSelector(_pullBuffsBehavior);
            }

            _combatBehavior = new LockSelector(_combatBehavior);

            _pullBehavior = new LockSelector(_pullBehavior);
            return true;
        }

        private bool EnsureComposite(bool error, BehaviorType type, out Composite composite)
        {
            Logger.WriteDebug("Creating " + type + " behavior.");
            int count = 0;
            composite = CompositeBuilder.GetComposite(Class, TalentManager.CurrentSpec, type, CurrentWoWContext,
                out count);
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