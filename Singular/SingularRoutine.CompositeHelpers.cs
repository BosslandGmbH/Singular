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


using System.Linq;

using CommonBehaviors.Actions;

using Singular.Composites;
using Singular.Settings;

using Styx;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Styx.Helpers;
using System;

using Action = TreeSharp.Action;
using System.Threading;
using Styx.WoWInternals;

namespace Singular
{
    public delegate WoWPoint LocationRetrievalDelegate(object context);
    
    partial class SingularRoutine
    {
        #region Delegates

        public delegate bool SimpleBoolReturnDelegate(object context);

        public delegate WoWUnit UnitSelectionDelegate(object context);

        #endregion

        /// <summary>Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        /// 		 going down to lower branches in the tree, while casting.)</summary>
        /// <remarks>Created 3/4/2011.</remarks>
        /// <returns>.</returns>
        protected Composite CreateWaitForCast()
        {
            return CreateWaitForCast(false);
        }

        /// <summary>Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        /// 		 going down to lower branches in the tree, while casting.)</summary>
        /// <remarks>Created 3/6/2011.</remarks>
        /// <param name="faceDuring">Whether or not to face during casting</param>
        /// <returns></returns>
        protected Composite CreateWaitForCast(bool faceDuring)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.IsCasting,
                    new PrioritySelector(
                        new Decorator(
                            ret => faceDuring && Me.CurrentTarget != null && !Me.IsSafelyFacing(Me.CurrentTarget, 70),
                            CreateFaceUnit()),
                        new ActionAlwaysSucceed()
                        )));
        }

        protected Composite CreateCastPetAction(PetAction action, bool parentIsSelector)
        {
            return CreateCastPetActionOn(action, parentIsSelector, ret => Me.CurrentTarget);
        }

        protected Composite CreateCastPetActionOn(PetAction action, bool parentIsSelector, UnitSelectionDelegate onUnit)
        {
            return new Action(
                delegate(object context)
                    {
                        PetManager.CastPetAction(action, onUnit(context));

                        // Purposely fail here, we want to 'skip' down the tree.
                        if (parentIsSelector)
                        {
                            return RunStatus.Failure;
                        }
                        return RunStatus.Success;
                    });
        }

		private static WaitTimer targetingTimer = new WaitTimer(TimeSpan.FromSeconds(2));

        protected Composite CreateEnsureTarget()
        {
            return
				new PrioritySelector(
					new Decorator(
						ret => NeedTankTargeting && targetingTimer.IsFinished && Me.Combat &&
							   TankTargeting.Instance.FirstUnit != null && Me.CurrentTarget != TankTargeting.Instance.FirstUnit,
						new Action(ret =>
							{
								Logger.WriteDebug("Targeting first unit of TankTargeting");
								TankTargeting.Instance.FirstUnit.Target();
								StyxWoW.SleepForLagDuration();
								targetingTimer.Reset();
							})),
					new Decorator(
						ret => Me.CurrentTarget == null || Me.CurrentTarget.Dead,
						new PrioritySelector(
							// Set our context to the RaF leaders target, or the first in the target list.
							ctx => RaFHelper.Leader != null && RaFHelper.Leader.Combat 
										? 
											RaFHelper.Leader.CurrentTarget 
										: 
											Targeting.Instance.FirstUnit != null && Targeting.Instance.FirstUnit.Combat
											?
												Targeting.Instance.FirstUnit
											:
                                                Me.Combat && ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Any(p => p.IsHostile && !p.IsOnTransport && !p.Dead && p.DistanceSqr <= 70 * 70)
                                                ?
                                                    ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(p => p.IsHostile && !p.IsOnTransport && !p.Dead && p.DistanceSqr <= 70 * 70).OrderBy(u => u.DistanceSqr).FirstOrDefault()
                                                :
                                                    null,
							// Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
							new Decorator(
								ret => ret != null,
								new Sequence(
									new Action(ret => Logger.Write("Target is invalid. Switching to " + ((WoWUnit)ret).Name + "!")),
									new Action(ret => ((WoWUnit)ret).Target()))),
							// In order to resolve getting "stuck" on a target, we'll clear it if there's nothing viable.
							new Action(
								ret =>
									{
										Me.ClearTarget();
										// Force a failure, just so we can move down the branch.
										return RunStatus.Failure;
									}),
							new ActionLogMessage(false, "No viable target! NOT GOOD!"))));
        }

        protected Composite CreateAutoAttack(bool includePet)
        {
			const int SPELL_ID_AUTO_SHOT = 75;

			return new PrioritySelector(
				new Decorator(
					ret => !Me.IsAutoAttacking && Me.AutoRepeatingSpellId != SPELL_ID_AUTO_SHOT,
					new Action(ret => Me.ToggleAttack())),
				new Decorator(
					ret => includePet && Me.GotAlivePet && (Me.Pet.CurrentTarget == null || Me.Pet.CurrentTarget != Me.CurrentTarget),
					new Action(delegate { PetManager.CastPetAction(PetAction.Attack); return RunStatus.Failure; }))
				);
        }

		protected Composite CreateUseWand()
		{
			return CreateUseWand(ret => true);
		}

		protected Composite CreateUseWand(SimpleBoolReturnDelegate extra)
		{
			return new PrioritySelector(
				new Decorator(
					ret => HasWand && !IsWanding && extra(ret),
					new Action(ret => SpellManager.Cast("Shoot")))
				);
		}

        public Composite CreateUseTrinketsBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.UseFirstTrinket,
                    new Decorator(
                        ret => Miscellaneous.UseTrinket(true),
                        new ActionAlwaysSucceed())),

                new Decorator(
                    ret => SingularSettings.Instance.UseSecondTrinket,
                    new Decorator(
                        ret => Miscellaneous.UseTrinket(false),
                        new ActionAlwaysSucceed()))
                );
        }

		public Composite CreateUseEquippedItem(uint slotId)
		{
			return new PrioritySelector(
				new Decorator(
					ret => Miscellaneous.UseEquippedItem(slotId),
					new ActionAlwaysSucceed()));
		}

        private void CastWithLog(string spellName, WoWUnit onTarget)
        {
            Logger.Write(string.Format("Casting {0} on {1}", spellName, (onTarget != null ? onTarget.SafeName() : "-Nobody-")));
            SpellManager.Cast(spellName, onTarget);
        }

        private void CastWithLog(int spellId, WoWUnit onTarget)
        {
            Logger.Write(string.Format("Casting {0} on {1}", WoWSpell.FromId(spellId).Name, onTarget.SafeName()));
            SpellManager.Cast(spellId, onTarget);
        }

        #region Cast By Name

        public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
			return CreateSpellCast(spellName, extra, unitSelector, true);
        }

		public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector, bool checkMoving)
		{
		    return new Decorator(
		        ret => extra(ret) && unitSelector(ret) != null && CanCast(spellName, unitSelector(ret), checkMoving),
		        new PrioritySelector(
		            CreateApproachToCast(spellName, unitSelector),
		            new Decorator(
		                ret => !checkMoving && Me.IsMoving && SpellManager.Spells[spellName].CastTime > 0,
		                new Sequence(
		                    new Action(ret => Navigator.PlayerMover.MoveStop()),
		                    new Action(ret => StyxWoW.SleepForLagDuration()))),
		            // Just logs the spell, and calls SpellManager.Cast(name) - Simply for readability, and to make sure
		            // manual spell logging is *all* the same.
		            new Action(ret => CastWithLog(spellName, unitSelector(ret)))));
		}

        public Composite CreateSpellCast(string spellName)
        {
			return CreateSpellCast(spellName, true);
        }

		public Composite CreateSpellCast(string spellName, bool checkMoving)
		{
			return CreateSpellCast(spellName, ret => true, checkMoving);
		}

        public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra)
        {
			return CreateSpellCast(spellName, extra, true);
        }

		public Composite CreateSpellCast(string spellName, SimpleBoolReturnDelegate extra, bool checkMoving)
		{
			return CreateSpellCast(spellName, extra, ret => Me.CurrentTarget, checkMoving);
		}

        public Composite CreateSpellCastOnSelf(string spellName)
        {
            return CreateSpellCast(spellName, ret => true);
        }

        public Composite CreateSpellCastOnSelf(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellName, extra, ret => Me);
        }

        #endregion

        #region Cast By ID

        public Composite CreateSpellCast(int spellId, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && SpellManager.CanCast(spellId, unitSelector(ret)),
                new Action(ret => CastWithLog(spellId, unitSelector(ret))));
        }

        public Composite CreateSpellCast(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellCast(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellCastOnSelf(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellCastOnSelf(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, ret => true, ret => Me);
        }

        #endregion

        #region Buff By Name

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            // BUGFIX: HB currently doesn't check ActiveAuras in the spell manager. So this'll break on new spell procs
            return CreateSpellBuff(spellName, extra, unitSelector, false);
        }

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector, bool waitForDebuff)
        {
            // BUGFIX: HB currently doesn't check ActiveAuras in the spell manager. So this'll break on new spell procs
            return
                new Sequence(
                    CreateSpellCast(
                spellName, ret => extra(ret) && unitSelector(ret) != null && !HasAuraStacks(spellName, 0, unitSelector(ret)), unitSelector, false),
                    new DecoratorContinue(
                        ret => waitForDebuff,
                        new Sequence(
                            new Action(ret => StyxWoW.SleepForLagDuration()),
                            new Action(ret => Thread.Sleep(100)),
                            new WaitContinue(3, ret => !Me.IsCasting, new Action(ret => StyxWoW.SleepForLagDuration())))));
        }

        public Composite CreateSpellBuff(string spellName)
        {
            return CreateSpellBuff(spellName, false);
        }

        public Composite CreateSpellBuff(string spellName, bool waitForDebuff)
        {
            return CreateSpellBuff(spellName, ret => true, waitForDebuff);
        }

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellBuff(spellName, extra, false);
        }

        public Composite CreateSpellBuff(string spellName, SimpleBoolReturnDelegate extra, bool waitForDebuff)
        {
            return CreateSpellBuff(spellName, extra, ret => Me.CurrentTarget, waitForDebuff);
        }

        public Composite CreateSpellBuffOnSelf(string spellName)
        {
            return CreateSpellBuffOnSelf(spellName, ret => true);
        }

        public Composite CreateSpellBuffOnSelf(string spellName, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellBuff(spellName, extra, ret => Me);
        }

        #endregion

        #region Cast By ID

        public Composite CreateSpellBuff(int spellId, SimpleBoolReturnDelegate extra, UnitSelectionDelegate unitSelector)
        {
            return new Decorator(
                ret => extra(ret) && unitSelector(ret) != null && SpellManager.CanBuff(spellId, unitSelector(ret)),
                new Action(ret => CastWithLog(spellId, unitSelector(ret))));
        }

        public Composite CreateSpellBuff(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellBuff(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me.CurrentTarget);
        }

        public Composite CreateSpellBuffOnSelf(int spellId)
        {
            return CreateSpellCast(spellId, ret => true);
        }

        public Composite CreateSpellBuffOnSelf(int spellId, SimpleBoolReturnDelegate extra)
        {
            return CreateSpellCast(spellId, extra, ret => Me);
        }

        #endregion

		#region Party Buff By Name

		/// <summary>
		/// To cast buffs on party members like Dampen Magic and such.
		/// </summary>
		/// <param name="spellName">Name of the buff</param>
		/// <returns></returns>
		public Composite CreateSpellPartyBuff(string spellName)
		{
			return
				new PrioritySelector(
					new Decorator(
						ret => Me.IsInParty && Me.PartyMembers.Any(p => p.IsAlive && !p.HasAura(spellName)),
						new PrioritySelector(
							ctx => Me.PartyMembers.First(p => p.IsAlive && !p.HasAura(spellName)),
							CreateMoveToAndFace(35, ret => (WoWUnit)ret),
							CreateSpellCast(spellName, ret => true, ret => (WoWUnit)ret)))
				);
		}

		#endregion

		#region ApproachToCast

		public Composite CreateApproachToCast(string spellName, UnitSelectionDelegate unitSelector)
		{
			return
				new Decorator(
					ret => SpellManager.Spells[spellName].MaxRange != 0 &&
						   (unitSelector(ret).Distance > SpellManager.Spells[spellName].MaxRange - 2f ||
							!unitSelector(ret).InLineOfSightOCD),
					new Action(ret => Navigator.MoveTo(unitSelector(ret).Location)));
		}

		#endregion

		#region CanCast

		public bool CanCast(string spellName, WoWUnit onUnit, bool checkMoving)
		{
			// Do we have spell?
			if (!SpellManager.HasSpell(spellName))
				return false;

			WoWSpell spell = SpellManager.Spells[spellName];

			// Use default CanCast if checkmoving is true
			if (checkMoving)
			{
                if (spell.CastTime != 0 && StyxWoW.Me.IsMoving)
                    return false;
			}

			// is spell in CD?
			if (spell.Cooldown)
			{
				return false;
			}

            // minrange check
            if (spell.MinRange != 0 && onUnit.DistanceSqr < spell.MinRange * spell.MinRange)
            {
                return false;
            }

			// are we casting or channeling ?
			if (Me.IsCasting || Me.ChanneledCastingSpellId != 0)
			{
				return false;
			}

			// do we have enough power?
			if (Me.GetCurrentPower(spell.PowerType) < spell.PowerCost)
			{
				return false;
			}

			// GCD check
			if (StyxWoW.GlobalCooldown)
			{
				return false;
			}

			// lua
			if (!spell.CanCast)
			{
				return false;
			}

			return true;
		}

		#endregion

        private Composite CreateSpellCastOnLocation(string spellName, LocationRetrievalDelegate onLocation)
        {
            return new Decorator(
                ret => CanCast(spellName, null, false),
                new Sequence(
                    new Action(ret => CastWithLog(spellName, null)),
                    new Action(ret => LegacySpellManager.ClickRemoteLocation(onLocation(ret)))));
        }

        /// <summary>Creates a composite to throw a throwing weapon, shoot a bow, crossbow, gun, or wand. If your inventory has such ranged weapon, and you know the skill.</summary>
        /// <remarks>Created 3/12/2011.</remarks>
        /// <returns>.</returns>
        public Composite CreateFireRangedWeapon()
        {
            return new PrioritySelector(
                CreateSpellCast(
                    "Throw", ret => Me.Inventory.Equipped.Ranged != null &&
                                    Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Thrown, false),
                CreateSpellCast(
                    "Shoot",
                    ret => Me.Inventory.Equipped.Ranged != null &&
                           (Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Bow ||
                            Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Crossbow ||
                            Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Gun ||
                            Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Wand), false)

                );
        }

        /// <summary>
        /// Creates a composite to use potions and healthstone.
        /// </summary>
        /// <param name="healthPercent">Healthpercent to use health potions and healthstone</param>
        /// <param name="manaPercent">Manapercent to use mana potions</param>
        /// <returns></returns>
        public Composite CreateUsePotionAndHealthstone(double healthPercent, double manaPercent)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.HealthPercent < healthPercent,
                    new PrioritySelector(
                        ctx => Me.CarriedItems.
                                    Where(i => 
                                        i != null &&
                                        i.Cooldown == 0 && 
                                        i.ItemInfo != null &&
                                        i.ItemInfo.RequiredLevel <= Me.Level &&
                                        i.ItemSpells != null &&
                                        i.ItemSpells.Any(s => 
                                                s.ActualSpell.Name == "Healthstone" ||
                                                s.ActualSpell.Name == "Healing Potion")).
                                    OrderBy(i => i.ItemInfo.Level).FirstOrDefault(),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))
                        )),
                new Decorator(
                    ret => Me.ManaPercent < manaPercent,
                    new PrioritySelector(
                        ctx => Me.CarriedItems.
                                    Where(i =>
                                        i != null &&
                                        i.Cooldown == 0 &&
                                        i.ItemInfo != null &&
                                        i.ItemInfo.RequiredLevel <= Me.Level &&
                                        i.ItemSpells != null &&
                                        i.ItemSpells.Any(s =>
                                                s.ActualSpell.Name == "Restore Mana")).
                                    OrderBy(i => i.ItemInfo.Level).FirstOrDefault(),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                new Action(ret => StyxWoW.SleepForLagDuration())))))
                );
        }
    }
}