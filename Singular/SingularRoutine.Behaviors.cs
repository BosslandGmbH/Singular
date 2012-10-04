using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;

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

        public bool RebuildBehaviors()
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
                Logger.Write("Using default rest behavior.");
                _restBehavior = Helpers.Rest.CreateDefaultRestBehaviour();
            }

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs, out _combatBuffsBehavior);
            // This is a small bugfix. Just to ensure we always pop trinkets, etc.
            if (_combatBuffsBehavior == null)
                _combatBuffsBehavior = new PrioritySelector();
            EnsureComposite(false, BehaviorType.Heal, out _healBehavior);
            EnsureComposite(false, BehaviorType.PullBuffs, out _pullBuffsBehavior);
            EnsureComposite(false, BehaviorType.PreCombatBuffs, out _preCombatBuffsBehavior);

            // Since we can be lazy, we're going to fix a bug right here and now.
            // We should *never* cast buffs while mounted. EVER. So we simply wrap it in a decorator, and be done with it.
            // 4/11/2012 - Changed to use a LockSelector to increased performance.
            if (_preCombatBuffsBehavior != null)
            {
                _preCombatBuffsBehavior =
                    new Decorator(
                        ret => !IsMounted && !Me.IsOnTransport && !SingularSettings.Instance.DisableNonCombatBehaviors,
                        new LockSelector(_preCombatBuffsBehavior));
            }

            if (_combatBuffsBehavior != null)
            {
                _combatBuffsBehavior = new Decorator(ret => !IsMounted && !Me.IsOnTransport,
                    new LockSelector( //Item.CreateUseAlchemyBuffsBehavior(),
                        // Item.CreateUseTrinketsBehavior(),
                        //Item.CreateUsePotionAndHealthstone(SingularSettings.Instance.PotionHealth, SingularSettings.Instance.PotionMana),
                        _combatBuffsBehavior));
            }

            // There are some classes that uses spells in rest behavior. Basicly we don't want Rest to be called while flying.
            if (_restBehavior != null)
            {
                _restBehavior =
                    new Decorator(
                        ret => !Me.IsFlying && !Me.IsOnTransport && !SingularSettings.Instance.DisableNonCombatBehaviors,
                        new LockSelector(_restBehavior));
            }

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