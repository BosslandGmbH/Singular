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
using System.Drawing;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;

using Singular.Composites;

using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Singular.Settings;

using TreeSharp;

using Action = TreeSharp.Action;

namespace Singular
{
    public delegate WoWPoint LocationRetrievalDelegate(object context);

    partial class SingularRoutine
    {
        #region Delegates

        public delegate bool SimpleBoolReturnDelegate(object context);

        public delegate WoWUnit UnitSelectionDelegate(object context);

        #endregion

        private static readonly WaitTimer targetingTimer = new WaitTimer(TimeSpan.FromSeconds(2));

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 3/4/2011.
        /// </remarks>
        /// <returns>.</returns>
        protected Composite CreateWaitForCast()
        {
            return CreateWaitForCast(false);
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. (Use this to prevent the CC from
        ///   going down to lower branches in the tree, while casting.)
        /// </summary>
        /// <remarks>
        ///   Created 3/6/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
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

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. Will also 
        ///   check spell target health and if >= SingularSettings.Instance.IgnoreHealTargetsAboveHealth
        ///   will stop the cast if its a heal.  Note: will cancel only heals cast using CreateSpellCast()
        ///   or a variant, or CastWithLog()
        /// </summary>
        /// <remarks>
        ///   Created 3/23/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <returns></returns>
        protected Composite CreateWaitForCastWithCancel()
        {
            return CreateWaitForCastWithCancel( SingularSettings.Instance.IgnoreHealTargetsAboveHealth);
        }

        /// <summary>
        ///   Creates a composite that will return a success, so long as you are currently casting. Will also 
        ///   check spell target health and if >= minHealth will stop the cast if its a heal.  Note: will cancel 
        ///   only heals cast using CreateSpellCast() or a variant, or CastWithLog()
        /// </summary>
        /// <remarks>
        ///   Created 3/23/2011.
        /// </remarks>
        /// <param name = "faceDuring">Whether or not to face during casting</param>
        /// <returns></returns>
        protected Composite CreateWaitForCastWithCancel(int minHealth)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => Me.IsCasting,
                    new PrioritySelector(
                        new Decorator(
                            ret =>  CastingSpellTarget == null
                                ||  (CastingSpellTarget is WoWPlayer && Me.IsHorde != ((WoWPlayer)CastingSpellTarget).IsHorde)
                                ||  !CastingSpellTarget.IsFriendly
                                ||  CastingSpellTarget.HealthPercent < minHealth
                                ||  Me.CastingSpell == null
                                ||  Me.CastingSpell.SpellEffect1 == null
                                ||  Me.CastingSpell.SpellEffect1.EffectType != WoWSpellEffectType.Heal,
                            new ActionAlwaysSucceed()),
                        new Action( delegate 
                            {
                                string spellName = Me.CastingSpell.Name;
                                double healthPct = CastingSpellTarget.HealthPercent;
                                SpellManager.StopCasting();
                                Logging.Write(Color.Orange, "[Singular] /cancelled {0} on {1} at {2:F0}%", spellName, CastingSpellTarget.SafeName(), healthPct);
                            }))
                    )
                );
        }

        protected Composite CreateCastPetAction(string action)
        {
            return CreateCastPetAction(action, ret => true);
        }

        protected Composite CreateCastPetAction(string action, SimpleBoolReturnDelegate extra)
        {
            return CreateCastPetActionOn(action, ret => Me.CurrentTarget, extra);
        }

        protected Composite CreateCastPetActionOn(string action, UnitSelectionDelegate onUnit)
        {
            return CreateCastPetActionOn(action, onUnit, ret => true);
        }

        protected Composite CreateCastPetActionOn(string action, UnitSelectionDelegate onUnit, SimpleBoolReturnDelegate extra)
        {
            return new Decorator(
                ret => extra(ret) && PetManager.CanCastPetAction(action),
                new Action(ret => PetManager.CastPetAction(action, onUnit(ret))));
        }

        protected Composite CreateCastPetActionOnLocation(string action)
        {
            return CreateCastPetActionOnLocation(action, ret => true);
        }

        protected Composite CreateCastPetActionOnLocation(string action, SimpleBoolReturnDelegate extra)
        {
            return CreateCastPetActionOnLocation(action, ret => Me.CurrentTarget.Location, extra);
        }

        protected Composite CreateCastPetActionOnLocation(string action, LocationRetrievalDelegate location)
        {
            return CreateCastPetActionOnLocation(action, location, ret => true);
        }

        protected Composite CreateCastPetActionOnLocation(string action, LocationRetrievalDelegate location, SimpleBoolReturnDelegate extra)
        {
            return new Decorator(
                ret =>  extra(ret) && PetManager.CanCastPetAction(action),
                new Sequence(
                    new Action(ret => PetManager.CastPetAction(action)),
                    new Action(ret => LegacySpellManager.ClickRemoteLocation(location(ret)))));
        }

        protected Composite CreateEnsureTarget()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => NeedTankTargeting && targetingTimer.IsFinished && Me.Combat &&
                               TankTargeting.Instance.FirstUnit != null && Me.CurrentTarget != TankTargeting.Instance.FirstUnit,
                        new Action(
                            ret =>
                                {
                                    Logger.WriteDebug("Targeting first unit of TankTargeting");
                                    TankTargeting.Instance.FirstUnit.Target();
                                    StyxWoW.SleepForLagDuration();
                                    targetingTimer.Reset();
                                })),
                    new Decorator(
                        ret => Me.CurrentTarget == null || Me.CurrentTarget.Dead,
                        new PrioritySelector(
                            ctx =>
                                {
                                    // If we have a RaF leader, then use its target.
                                    if (RaFHelper.Leader != null && RaFHelper.Leader.Combat)
                                    {
                                        return RaFHelper.Leader.CurrentTarget;
                                    }

                                    // Does the target list have anything in it? And is the unit in combat?
                                    // Make sure we only check target combat, if we're NOT in a BG. (Inside BGs, all targets are valid!!)
                                    if (Targeting.Instance.FirstUnit != null && Me.Combat)
                                    {
                                        return Targeting.Instance.FirstUnit;
                                    }
                                    // Cache this query, since we'll be using it for 2 checks. No need to re-query it.
                                    var units =
                                        ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(
                                            p => p.IsHostile && !p.IsOnTransport && !p.Dead && p.DistanceSqr <= 70 * 70 && p.Combat);

                                    if (Me.Combat && units.Any())
                                    {
                                        // Return the closest one to us
                                        return units.OrderBy(u => u.DistanceSqr).FirstOrDefault();
                                    }

                                    // And there's nothing left, so just return null, kthx.
                                    return null;
                                },
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
                                        // Force a failure, just so we can move down the branch. to the log message
                                        return RunStatus.Failure;
                                    }),
                            new ActionLogMessage(false, "No viable target! NOT GOOD!"),
                            new ActionAlwaysSucceed())));
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
                    new Action(
                        delegate
                            {
                                PetManager.CastPetAction("Attack");
                                return RunStatus.Failure;
                            }))
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

        protected void CastWithLog(string spellName, WoWUnit onTarget)
        {
            CastingSpellTarget = onTarget; // save current spell target, reset by SPELL_CAST_SUCCESS event
            Logger.Write(string.Format("Casting {0} on {1}", spellName, (onTarget != null ? onTarget.SafeName() : "-Nobody-")));
            if (onTarget == null)
                SpellManager.Cast(spellName);
            else
                SpellManager.Cast(spellName, onTarget);
        }

        protected void CastWithLog(int spellId, WoWUnit onTarget)
        {
            CastingSpellTarget = onTarget; // save current spell target, reset by SPELL_CAST_SUCCESS event
            Logger.Write(string.Format("Casting {0} on {1}", WoWSpell.FromId(spellId).Name, (onTarget != null ? onTarget.SafeName() : "-Nobody-")));
            if (onTarget == null)
                SpellManager.Cast(spellId);
            else
                SpellManager.Cast(spellId, onTarget);
        }

        private Composite CreateSpellCastOnLocation(string spellName, LocationRetrievalDelegate onLocation)
        {
            return new Decorator(
                ret => CanCast(spellName, null, false),
                new Sequence(
                    new Action(ret => CastWithLog(spellName, null)),
                    new Action(ret => StyxWoW.SleepForLagDuration()),
                    new Action(ret => LegacySpellManager.ClickRemoteLocation(onLocation(ret)))));
        }

        /// <summary>
        ///   Creates a composite to throw a throwing weapon, shoot a bow, crossbow, gun, or wand. If your inventory has such ranged weapon, and you know the skill.
        /// </summary>
        /// <remarks>
        ///   Created 3/12/2011.
        /// </remarks>
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
                        spellName, ret => extra(ret) && unitSelector(ret) != null && !HasAuraStacks(spellName, 0, unitSelector(ret)), unitSelector,
                        false),
                    new DecoratorContinue(
                        ret => waitForDebuff,
                        new Sequence(
                            new WaitContinue(1, ret => Me.IsCasting,
                                new ActionAlwaysSucceed()),
                            new WaitContinue(3, ret => !Me.IsCasting && !StyxWoW.GlobalCooldown,
                                new Action(ret =>
                                    {
                                        StyxWoW.SleepForLagDuration();
                                        Thread.Sleep(100);
                                    })))));
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
        ///   To cast buffs on party members like Dampen Magic and such.
        /// </summary>
        /// <param name = "spellName">Name of the buff</param>
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
            {
                return false;
            }

            WoWSpell spell = SpellManager.Spells[spellName];

            // Use default CanCast if checkmoving is true
            if (checkMoving)
            {
                if (spell.CastTime != 0 && StyxWoW.Me.IsMoving)
                {
                    return false;
                }
            }

            // is spell in CD?
            if (spell.Cooldown)
            {
                return false;
            }

            // minrange check
            if (spell.MinRange != 0 && onUnit != null && onUnit.DistanceSqr < spell.MinRange * spell.MinRange)
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
            if (StyxWoW.GlobalCooldown && !Me.HasAura("Adrenaline Rush"))
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
    }
}