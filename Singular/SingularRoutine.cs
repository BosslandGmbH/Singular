using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        private Composite _combatBehavior;

        private Composite _combatBuffsBehavior;

        private Composite _healBehavior;

        private Composite _preCombatBuffsBehavior;

        private Composite _pullBehavior;

        private Composite _pullBuffsBehavior;

        private Composite _restBehavior;

        public override string Name { get { return "Singular"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton
        {
            get { return true; }
        }
        public override void OnButtonPress()
        {
            new GUI.ConfigurationForm().ShowDialog();
        }

        public override void Pulse()
        {
            HealTargeting.Instance.Pulse();
        }

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

        /// <summary>Gets the nearby friendly players within 40 yards.</summary>
        /// <value>The nearby friendly players.</value>
        public List<WoWPlayer> NearbyFriendlyPlayers { get { return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Where(p => p.DistanceSqr <= 40 * 40 && p.IsFriendly).ToList(); } }

        /// <summary>Gets the nearby unfriendly units within 40 yards.</summary>
        /// <value>The nearby unfriendly units.</value>
        public List<WoWUnit> NearbyUnfriendlyUnits { get { return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(p => p.IsHostile && !p.Dead && !p.IsPet && p.DistanceSqr <= 40 * 40).ToList(); } }

        public bool CurrentTargetIsEliteOrBoss { get { return Me.CurrentTarget.Elite; } }

        public bool IsMounted
        {
            get
            {
                switch (StyxWoW.Me.Shapeshift)
                {
                    case ShapeshiftForm.FlightForm:
                    case ShapeshiftForm.EpicFlightForm:
                        return true;
                }
                return StyxWoW.Me.Mounted;
            }
        }

        public override void Initialize()
        {
            Logger.Write("Starting Singular v" + Assembly.GetExecutingAssembly().GetName().Version);
            AttachEventHandlers();
            Logger.Write("Determining talent spec.");
            try
            {
                TalentManager.Update();
            }
            catch (Exception e)
            {
                StopBot(e.ToString());
            }
            Logger.Write("Current spec is " + TalentManager.CurrentSpec.ToString().CamelToSpaced());

            if (!CreateBehaviors())
            {
                return;
            }
            Logger.Write("Behaviors created!");
        }

        public bool CreateBehaviors()
        {
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
                // By default, eat/drink at 50%
                _restBehavior = CreateDefaultRestComposite(50, 50);
            }

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs, out _combatBuffsBehavior);
            EnsureComposite(false, BehaviorType.Heal, out _healBehavior);
            EnsureComposite(false, BehaviorType.PullBuffs, out _pullBuffsBehavior);

            EnsureComposite(false, BehaviorType.PreCombatBuffs, out _preCombatBuffsBehavior);

            // Since we can be lazy, we're going to fix a bug right here and now.
            // We should *never* cast buffs while mounted. EVER. So we simply wrap it in a decorator, and be done with it.
            _preCombatBuffsBehavior = new Decorator(ret => !IsMounted, _preCombatBuffsBehavior);

            return true;
        }

        private bool EnsureComposite(bool error, BehaviorType type, out Composite composite)
        {
            Logger.WriteDebug("Creating " + type + " behavior.");
            composite = CompositeBuilder.GetComposite(this, Class, TalentManager.CurrentSpec, type, CurrentWoWContext);
            if (composite == null && error)
            {
                StopBot(
                    string.Format(
                        "Singular currently does not support {0} for this class/spec combination, in this context! [{1}, {2}, {3}]", type, Class,
                        TalentManager.CurrentSpec, CurrentWoWContext));
                return false;
            }
            return composite != null;
        }

        private static void StopBot(string reason)
        {
            Logger.Write(reason);
            TreeRoot.Stop();
        }

        public bool IsCrowdControlled(WoWUnit unit)
        {
            return unit.GetAllAuras().Any(
                a => a.IsHarmful &&
                     (a.Spell.Mechanic == WoWSpellMechanic.Shackled ||
                      a.Spell.Mechanic == WoWSpellMechanic.Polymorphed ||
                      a.Spell.Mechanic == WoWSpellMechanic.Horrified ||
                      a.Spell.Mechanic == WoWSpellMechanic.Rooted ||
                      a.Spell.Mechanic == WoWSpellMechanic.Frozen ||
                      a.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                      a.Spell.Mechanic == WoWSpellMechanic.Fleeing ||
                      a.Spell.Mechanic == WoWSpellMechanic.Banished ||
                      a.Spell.Mechanic == WoWSpellMechanic.Sapped));
        }

        public bool HasAuraStacks(string aura, int stacks, WoWUnit unit)
        {
            // Active auras first.
            if (unit.ActiveAuras.ContainsKey(aura))
                return unit.ActiveAuras[aura].StackCount >= stacks;

            // Check passive shit. (Yep)
            if (unit.Auras.ContainsKey(aura))
                return unit.ActiveAuras[aura].StackCount >= stacks;

            return false;
        }
        public bool HasAuraStacks(string aura, int stacks)
        {
            return HasAuraStacks(aura, stacks, Me);
        }
    }
}