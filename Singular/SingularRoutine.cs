#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Singular.GUI;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Styx.Helpers;

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

		public static SingularRoutine Instance { get; set; }

        public override string Name { get { return "Singular $Revision$"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton { get { return true; } }

        public LocalPlayer Me { get { return StyxWoW.Me; } }

		public WoWClass myClass { get; set; }

		public SingularRoutine()
		{
			Instance = this;
		}

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

        /// <summary>
        ///   Gets the nearby friendly players within 40 yards.
        /// </summary>
        /// <value>The nearby friendly players.</value>
        public List<WoWPlayer> NearbyFriendlyPlayers { get { return ObjectManager.GetObjectsOfType<WoWPlayer>(true, true).Where(p => p.DistanceSqr <= 40 * 40 && p.IsFriendly).ToList(); } }

        /// <summary>
        ///   Gets the nearby unfriendly units within 40 yards.
        /// </summary>
        /// <value>The nearby unfriendly units.</value>
        public List<WoWUnit> NearbyUnfriendlyUnits
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(p => p.IsHostile && !p.Dead && !p.IsPet && p.DistanceSqr <= 40 * 40).
                        ToList();
            }
        }

        public bool CurrentTargetIsElite { get { return Me.CurrentTarget.Elite; } }

		public bool CurrentTargetIsBoss { get { return BossIds.Contains(Me.CurrentTarget.Entry); } }

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

        public override void OnButtonPress()
        {
            new ConfigurationForm().ShowDialog();
        }

        public bool NeedHealTargeting { get; set; }

        public bool NeedTankTargeting { get; set; }

        public override void Pulse()
        {
			if (NeedHealTargeting)
				HealTargeting.Instance.Pulse();
			if (NeedTankTargeting && (Me.IsInParty || Me.IsInRaid))
				TankTargeting.Instance.Pulse();

			//This is here to support character changes while HB is running :)
			if (Class != myClass && Class != WoWClass.None)
			{
				myClass = Class;
				Logger.Write("Character changed. New character: " + myClass.ToString() + ". Rebuilding behaviors");			
				TalentManager.Update();
				CharacterSettings.Instance.Load();
				CreateBehaviors();
			}
        }

        public override void Initialize()
        {
			//Caching current class here to avoid issues with loading screens where Class return None and we cant build behaviors
			myClass = Me.Class;

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

		private WoWContext lastContext { get; set; }

        public bool CreateBehaviors()
        {
			//Caching the context to not recreate same behaviors repeatedly.
			lastContext = CurrentWoWContext;

            NeedHealTargeting = false;
            NeedTankTargeting = false;

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
            if (_preCombatBuffsBehavior != null)
                _preCombatBuffsBehavior = new Decorator(ret => !IsMounted && !Me.IsOnTransport, _preCombatBuffsBehavior);
            if (_combatBuffsBehavior != null)
				_combatBuffsBehavior = new Decorator(ret => !IsMounted && !Me.IsOnTransport, _combatBuffsBehavior);

            return true;
        }

        private bool EnsureComposite(bool error, BehaviorType type, out Composite composite)
        {
            Logger.WriteDebug("Creating " + type + " behavior.");
            composite = CompositeBuilder.GetComposite(this, myClass, TalentManager.CurrentSpec, type, CurrentWoWContext);
            if (composite == null && error)
            {
                StopBot(
                    string.Format(
                        "Singular currently does not support {0} for this class/spec combination, in this context! [{1}, {2}, {3}]", type, myClass,
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

		/// <summary>
		/// Checks if there is an aura created by you on the target. Useful for DoTs.
		/// 	
		/// Warning: This only checks your own auras on the unit !
		/// </summary>
		/// <param name="aura">Name of the spell</param>
		/// <param name="unit">Unit to check</param>
		/// <returns></returns>
		public bool HasMyAura(string aura, WoWUnit unit)
		{
			return HasMyAura(aura, unit, TimeSpan.Zero, 0);
		}

		/// <summary>
		/// Checks if there is an aura created by you on the target. Useful for DoTs.
		/// This will return false even while you have the aura on the unit but the timeleft is lower then the expire time.
		/// Useful to cast DoTs before expiring
		/// 
		/// Warning: This only checks your own auras on the unit !
		/// </summary>
		/// <param name="aura">Name of the spell</param>
		/// <param name="unit">Unit to check</param>
		/// <param name="timeLeft">Time left for the aura.</param>
		/// <returns></returns>
		public bool HasMyAura(string aura, WoWUnit unit, TimeSpan timeLeft)
		{
			return HasMyAura(aura, unit, timeLeft, 0);
		}

		/// <summary>
		/// Checks if there is an aura created by you on the target. Useful for DoTs.
		/// This will return false even while you have the aura on the unit but the stackcount is lower then provided value.
		/// Useful to stack more aura on the unit
		/// </summary>
		/// <param name="aura">Name of the spell</param>
		/// <param name="unit">Unit to check</param>
		/// <param name="stackCount">Stack count</param>
		/// <returns></returns>
		public bool HasMyAura(string aura, WoWUnit unit, int stackCount)
		{
			return HasMyAura(aura, unit, TimeSpan.Zero, stackCount);
		}

		/// <summary>
		/// Checks if there is an aura created by you on the target. Useful for DoTs.
		/// This will return false even while you have the aura on the unit but the stackcount is lower then provided value and
		/// timeleft is lower then the expire time.
		/// Useful to stack more dots or redot before the aura expires.
		/// </summary>
		/// <param name="aura">Name of the spell</param>
		/// <param name="unit">Unit to check</param>
		/// <param name="timeLeft">Time left for the aura.</param>
		/// <param name="stackCount">Stack count</param>
		/// <returns></returns>
		public bool HasMyAura(string aura, WoWUnit unit, TimeSpan timeLeft, int stackCount)
		{
			// Check for unit being null first, so we don't end up with an exception
			if (unit == null)
			{
				return false;
			}

			// If the unit has that aura and it has been created by us return true
			if (unit.ActiveAuras.ContainsKey(aura))
			{
				var _aura = unit.ActiveAuras[aura];

				if (_aura.CreatorGuid == Me.Guid && _aura.TimeLeft > timeLeft && _aura.StackCount >= stackCount)
				{
					return true;
				}
			}

			return false;
		}

        public TimeSpan GetAuraTimeLeft(string auraName, WoWUnit onUnit, bool fromMyAura)
        {
            var wantedAura = onUnit.GetAllAuras().Where(a => a.Name == auraName && (fromMyAura ? a.CreatorGuid == Me.Guid : true)).FirstOrDefault();

            if (wantedAura!=null)
                return wantedAura.TimeLeft;
            return TimeSpan.Zero;
        }

        public bool HasAuraStacks(string aura, int stacks, WoWUnit unit)
        {
            // Active auras first.
            if (unit.ActiveAuras.ContainsKey(aura))
            {
                return unit.ActiveAuras[aura].StackCount >= stacks;
            }

            // Check passive shit. (Yep)
            if (unit.Auras.ContainsKey(aura))
            {
                return unit.Auras[aura].StackCount >= stacks;
            }

            // Try just plain old auras...
            if (stacks == 0)
            {
                return unit.HasAura(aura);
            }

            return false;
        }

        public bool HasAuraStacks(string aura, int stacks)
        {
            return HasAuraStacks(aura, stacks, Me);
        }

		public bool HasWand
		{
			get
			{
				return Me.Inventory.Equipped.Ranged != null &&
					   Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Wand;
			}
		}

		public bool IsWanding
		{
			get
			{
				return Me.AutoRepeatingSpellId == 5019;
			}
		}
    }
}