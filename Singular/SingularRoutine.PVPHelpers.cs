
using System.Linq;

using TreeSharp;
using Styx.Logic.Combat;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Singular.Settings;
using Styx;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx.Logic.Pathing;
using Styx.Logic.POI;
namespace Singular
{
    public enum PvPTrinketSlot
    {
        Slot1,
        Slot2,
        None
    }

    public class PVPPlayerState
    {
        private bool rooted;

        // Don't try to kill this, better find another target, don't waste CD's/mana/procs on it
        public bool Invulnerable { get; private set; }
        // Used for mages, to detect when to use 'Lance' and 'Frost Bolt'
        public bool Freezed { get; private set; }
        // target stunned, damage freely
        public bool Stunned { get; private set; }
        // target feared, feel free to damage it
        public bool Feared { get; private set; }
        // target under poly like control, use if need to engage in melee, don't break if far
        public bool Incapacitated { get; private set; }
        // don't cast valuable spells/procs on target
        public bool ResistsBinarySpells { get; private set; }
        // don't cast stuns on target
        public bool ResistsStun { get; private set; }
        // don't cast, otherwise it will kill u))) (Warriors)
        public bool ReflectMagic { get; private set; }
        // don't fear it
        public bool ResistsFear { get; private set; }
        // it's already slowed, don't cast any more slow stuff on it
        public bool Slowed { get; private set; }
        // target cannot move
        public bool Rooted
        {
            get
            {
                return rooted || Freezed;
            }

            private set
            {
                rooted = value;
            }
        }

        public PVPPlayerState(WoWPlayer player)
        {
            if (player == null)
                return;

            // Cycle trough target auras
            foreach (KeyValuePair<string, WoWAura> aura in player.Auras)
            {
                if (aura.Key.Equals("Sap") ||
                    aura.Key.Equals("Polymorph") ||
                    aura.Key.Equals("Ring of Frost") ||
                    aura.Key.Equals("Repentance") ||
                    aura.Key.Equals("Hungering Cold") ||
                    aura.Key.Equals("Blind"))
                {
                    Incapacitated = true;
                    Logger.Write("Target is Incapacitated: " + aura.Key);
                }
                else if (aura.Key.Equals("Ice Block") ||
                    aura.Key.Equals("Divine Shield") || 
                    aura.Key.Equals("Cyclone"))
                {
                    Invulnerable = true;
                    Logger.Write("Target is Invulnerable: " + aura.Key);
                }
                else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                    aura.Key.Equals("Deep Freeze") || 
                    aura.Key.Equals("Hammer of Justice") ||
                    aura.Key.Equals("Pounce") ||
                    aura.Key.Equals("Maim"))
                {
                    Stunned = true;
                    Logger.Write("Target is stunned: " + aura.Key);
                }
                else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                    aura.Value.Spell.Mechanic == WoWSpellMechanic.Snared ||
                    aura.Key.Equals("Frostfire Bolt") ||
                    aura.Key.Equals("Chains of Ice") ||
                    aura.Key.Equals("Aftermath") ||
                    aura.Key.Equals("Permafrost") || 
                    aura.Key.Equals("Piercing Chill") ||
                    aura.Key.Equals("Hamstring") ||
                    aura.Key.Equals("Typhoon") ||
                    aura.Key.Equals("Frost Shock") ||
                    aura.Key.Equals("Frozen Power"))
                {
                    Slowed = true;
                    Logger.Write("Target is slowed: " + aura.Key);
                }
                else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted)
                {
                    rooted = true;
                    Logger.Write("Target is rooted: " + aura.Key);
                }
                else if (aura.Key.Equals("Fear") ||
                    aura.Key.Equals("Psychic Scream") ||
                    aura.Key.Equals("Howl of Terror") ||
                    aura.Key.Equals("Intimidating Shout"))
                {
                    Feared = true;
                    Logger.Write("Target is feared: " + aura.Key);
                }
                else if (aura.Key.Equals("Anti-Magic Shell") ||
                    aura.Key.Equals("Cloak of Shadows"))
                {
                    ResistsBinarySpells = true;
                    Logger.Write("Target resists binary spell: " + aura.Key);
                }
                else if (aura.Key.Equals("Icebound Fortitude"))
                {
                    ResistsStun = true;
                    Logger.Write("Target resists stun!");
                }
                else if (aura.Key.Equals("Spell Reflect"))
                {
                    ReflectMagic = true;
                    Logger.Write("Target has spell reflect");
                }

                if (aura.Key.Equals("Frost Nova") ||
                    aura.Key.Equals("Deep Freeze") ||
                    aura.Key.Equals("Improved Cone of Cold"))
                {
                    Freezed = true;
                }
            }
        }
    }

    partial class SingularRoutine
    {

        #region Melee targeting variables

        private WaitTimer targetAwayFromMeleeTimer = new WaitTimer(System.TimeSpan.FromSeconds(10));
        private WaitTimer targetSwitchTimer = new WaitTimer(System.TimeSpan.FromSeconds(5));
        private ulong targetAwayFromMeleeGuid = 0;
        private List<WoWPlayer> unitsAt40Range;

        #endregion

        #region Describing player state variables

        // can use blink or anti-stun spells
        public bool MeUnderStunLikeControl { get; set; }
        // Cannot use "blink" etc under this control
        public bool MeUnderSheepLikeControl { get; set; }
        // Cannot use blink, but can use antifear
        public bool MeUnderFearLikeControl { get; set; }
        // I move slowly
        public bool MeSlowed { get; set; }
        // I cannot move
        public bool MeRooted { get; set; }

        #endregion

        #region Describing target state variables

        public PVPPlayerState TargetState { get; set; }

        #endregion 

        #region Check player state composites

        protected Composite CreateCheckPlayerPvPState()
        {
            return new Action(ret =>
                {
                    // Reset all values
                    MeUnderStunLikeControl = false;
                    MeUnderSheepLikeControl = false;
                    MeUnderFearLikeControl = false;
                    MeRooted = false;
                    MeSlowed = false;

                    // Cycle trough my auras
                    foreach (KeyValuePair<string, WoWAura> aura in Me.Auras)
                    {
                        if (aura.Key.Equals("Polymorh") ||
                            aura.Key.Equals("Ring of Frost") ||
                            aura.Key.Equals("Repentance") ||
                            aura.Key.Equals("Hungering Cold") ||
                            aura.Key.Equals("Blind"))
                        {
                            MeUnderSheepLikeControl = true;
                            Logger.Write("I'm under SHEEP like control: " + aura.Key);
                        }
                        else if (aura.Key.Equals("Fear") ||
                            aura.Key.Equals("Psychic Scream") ||
                            aura.Key.Equals("Howl of Terror") ||
                            aura.Key.Equals("Intimidating Shout"))
                        {
                            MeUnderFearLikeControl = true;
                            Logger.Write("I'm under FEAR like control: " + aura.Key);
                        }
                        else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                            aura.Key.Equals("Deep Freeze") || 
                            aura.Key.Equals("Pounce") ||
                            aura.Key.Equals("Maim") ||
                            aura.Key.Equals("Hammer of Justice"))
                        {
                            MeUnderStunLikeControl = true;
                            Logger.Write("I'm under STUN like control: " + aura.Key);
                        }
                        else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed ||
                            aura.Value.Spell.Mechanic == WoWSpellMechanic.Snared ||
                            aura.Key.Equals("Frostfire Bolt") ||
                            aura.Key.Equals("Chains of Ice") ||
                            aura.Key.Equals("Aftermath") ||
                            aura.Key.Equals("Permafrost") ||
                            aura.Key.Equals("Piercing Chill") ||
                            aura.Key.Equals("Hamstring") ||
                            aura.Key.Equals("Typhoon") ||
                            aura.Key.Equals("Frost Shock"))
                        {
                            MeSlowed = true;
                            Logger.Write("I'm SLOWED: " + aura.Key);
                        }
                        else if (aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted ||
                            aura.Key.Equals("Improved Cone of Cold") ||
                            aura.Key.Equals("Frost Nova") ||
                            aura.Key.Equals("Frozen Power"))
                        {
                            MeRooted = true;
                            Logger.Write("I'm ROOTED: " + aura.Key);
                        }
                        else if (aura.Key.Equals("Ice Block") ||
                            aura.Key.Equals("Divine Shield"))
                        {
                            Logger.Write("I'm invulnerable: " + aura.Key);
                        }

                    }

                    return RunStatus.Failure;
                });
        }

        #endregion

        #region Check target state composites/functions

        protected Composite CreateCheckTargetPvPState()
        {
            return new Action(ret =>
                {
                    TargetState = new PVPPlayerState(Me.CurrentTarget is WoWPlayer ? (WoWPlayer)Me.CurrentTarget : null);
                    return RunStatus.Failure;
                });
        }

        #endregion

        #region Use remove control ability/trinket

        protected Composite CreateUseAntiPvPControl()
        {
            return new PrioritySelector(
                // Detect if it's human, try to 
                new Decorator(ret => SpellManager.HasSpell("Every Man for Himself"),
                    new PrioritySelector(
                        CreateSpellBuffOnSelf("Every Man for Himself"),
                        new Action(ret => { return RunStatus.Success; }))),
                new Decorator(ret => SingularSettings.Instance.PvPTrinketSlot != PvPTrinketSlot.None,
                    new PrioritySelector(
                        new Decorator(
                            ret => SingularSettings.Instance.PvPTrinketSlot == PvPTrinketSlot.Slot1 &&
                                   StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown == 0,
                            new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket1.Use())),
                        new Decorator(
                            ret => SingularSettings.Instance.PvPTrinketSlot == PvPTrinketSlot.Slot2 &&
                                   StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown == 0,
                            new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket2.Use())))));
        }

        #endregion

        #region Use trinket/ability which increases damage

        /**
         * Might return failure, so insert ActionAlwaysSucceed after this composite if you need to stop the tree
         */
        protected Composite CreateUsePvPDamageIncreaseAbility()
        {
            // probably it's needed to insert some racials, like troll berserk or smth...
            return new PrioritySelector(
                new Decorator(ret => SingularSettings.Instance.PvPDamageTrinketSlot != PvPTrinketSlot.None,
                    new PrioritySelector(
                        new Decorator(
                            ret => SingularSettings.Instance.PvPDamageTrinketSlot == PvPTrinketSlot.Slot1 &&
                                StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown == 0,
                            new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket1.Use())),
                        new Decorator(
                            ret => SingularSettings.Instance.PvPDamageTrinketSlot == PvPTrinketSlot.Slot2 &&
                                StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown == 0,
                            new Action(ret => StyxWoW.Me.Inventory.Equipped.Trinket2.Use())))));
        }

        #endregion

        #region Melee targeting


        protected Composite CreateEnsurePVPTargetMelee()
        {
            return
                new PrioritySelector(
                // check if timer elapsed.. try to find closer target within melee range
                    new Decorator(
                        ret => Me.CurrentTarget != null && targetAwayFromMeleeTimer.IsFinished &&
                            Me.CurrentTarget.Guid == targetAwayFromMeleeGuid && 
                            !(Me.CurrentTarget.HealthPercent <= 20 && Me.CurrentTarget.DistanceSqr <= 30 * 30),
                        new Action(
                            ret =>
                            {
                                // try to find any target within melee range
                                WoWPlayer unit = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Where(
                                    p => p.IsHostile && !p.Dead && p.DistanceSqr <= (10 * 10)).OrderBy(
                                        u => u.HealthPercent).FirstOrDefault();
                                if (unit != null)
                                {
                                    Logger.Write("[Melee timer timeout] Found more suitable unit: " + unit.Name);
                                    TargetUnit(unit);
                                    targetAwayFromMeleeGuid = unit.Guid;
                                    targetAwayFromMeleeTimer.Reset();
                                    return RunStatus.Success;
                                }
                                else
                                {
                                    Logger.Write("[Melee timer timeout] Didn't find any suitable unit.");
                                    return RunStatus.Failure;
                                }
                            })),
                    // Make sure we have correct, target, if we don't find new target within range.
                    new Decorator(
                        ret => Me.CurrentTarget == null || Me.CurrentTarget.DistanceSqr > (35 * 35) || 
                            !Me.CurrentTarget.IsAlive,
                        new Action(
                            ret =>
                            {
                                //get nearest one
                                WoWPlayer unit = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Where(
                                    p => p.IsHostile && !p.Dead && p.DistanceSqr <= (35 * 35)).OrderBy(
                                        u => u.DistanceSqr).FirstOrDefault();

                                if (unit != null)
                                {
                                    Logger.Write("[Invalid target] Found new target: " + unit.Name);
                                    TargetUnit(unit);
                                    targetAwayFromMeleeGuid = unit.Guid;
                                    targetAwayFromMeleeTimer.Reset();
                                    return RunStatus.Success;
                                }
                                else
                                {
                                    Logger.Write("[Invalid target] Didn't find any targets");
                                    return RunStatus.Failure;
                                }
                            })),
                    new Decorator(
                        ret => Me.CurrentTarget != null && Me.CurrentTarget.DistanceSqr <= (10 * 10),
                        new Action(ret =>
                        {
                            if (targetAwayFromMeleeGuid != Me.CurrentTarget.Guid)
                            {
                                targetAwayFromMeleeGuid = Me.CurrentTarget.Guid;
                            }

                            targetAwayFromMeleeTimer.Reset();
                            return RunStatus.Failure;
                        })),
                    new Action(ret => { return RunStatus.Failure; }));
        }

        #endregion

        #region Range DD targeting

        protected Composite CreateEnsurePVPTargetRanged()
        {
            return new Action(ret =>
                {
                    unitsAt40Range = ObjectManager.GetObjectsOfType<WoWPlayer>(false, false).Where(
                        p => p.IsHostile && !p.Dead && p.DistanceSqr <= (40 * 40)).ToList();

                    if (unitsAt40Range == null)
                        return RunStatus.Failure;

                    if (Me.CurrentTarget == null || Me.CurrentTarget.DistanceSqr > (45 * 45) ||
                            !Me.CurrentTarget.IsAlive)
                    {
                        // Pick up closes one
                        WoWPlayer unit = unitsAt40Range.OrderBy(u => u.DistanceSqr).FirstOrDefault();
                        if (unit != null)
                        {
                            targetSwitchTimer.Reset();
                            TargetUnit(unit);
                            Logger.Write("[Invalid target] Found new target: " + unit.Name);
                            return RunStatus.Success;
                        }
                        else
                        {
                            return RunStatus.Failure;
                        }

                    }

                    if (!targetSwitchTimer.IsFinished || Me.IsCasting)
                        return RunStatus.Failure;

                    double currentTargetDistanceSqr = Me.CurrentTarget != null ? Me.CurrentTarget.DistanceSqr : 40 * 40;

                    for (uint i = 2; i < 8; i++ )
                    {
                        double rangeSqr = (i * 5) * (i * 5);

                        if (rangeSqr > currentTargetDistanceSqr)
                        {
                            return RunStatus.Failure;
                        }

                        List<WoWPlayer> unitsAtRange = unitsAt40Range.Where(p => p.DistanceSqr <= rangeSqr).ToList();
                        if (unitsAtRange.Count > 0)
                        {
                            WoWPlayer unit = unitsAtRange.OrderBy(u => u.HealthPercent).FirstOrDefault();

                            if (unit != null)
                            {
                                targetSwitchTimer.Reset();
                                TargetUnit(unit);
                                Logger.Write("[Invalid target] Found new target: " + unit.Name + " I: "+ i);
                                return RunStatus.Success;
                            }
                            else
                            {
                                return RunStatus.Failure;
                            }
                        }
                    }

                    return RunStatus.Failure;
                });
        }

        #endregion

        #region Helper targeting functions

        protected void TargetUnit(WoWUnit unit)
        {
            BotPoi.Current = new BotPoi(unit, PoiType.Kill);
            unit.Target();
        }

        #endregion

        #region PvP Movement Routine

        protected Composite CreateMoveToAndFacePvP()
        {
            return CreateMoveToAndFacePvP(5f, 70, ret => Me.CurrentTarget, false, RunStatus.Failure);
        }

        protected Composite CreateMoveToAndFacePvP(float maxRange, float coneDegrees, UnitSelectionDelegate unit, bool noMovement, RunStatus result)
        {
            return new Decorator(
                ret => unit(ret) != null,
                new Action(
                    ret =>
                    {
                        if (!SingularSettings.Instance.DisableAllMovement &&
                            (!unit(ret).InLineOfSightOCD || (!noMovement && unit(ret).DistanceSqr > maxRange * maxRange)))
                        {
                            Navigator.MoveTo(unit(ret).Location);
                            return result;
                            //return RunStatus.Success;
                        }
                        else if (!SingularSettings.Instance.DisableAllMovement && Me.IsMoving &&
                            unit(ret).DistanceSqr <= maxRange * maxRange)
                        {
                            Navigator.PlayerMover.MoveStop();
                            return RunStatus.Failure;
                            //StyxWoW.SleepForLagDuration();
                        }
                        else if (Me.CurrentTarget != null && Me.CurrentTarget.IsAlive &&
                                !Me.IsSafelyFacing(Me.CurrentTarget, coneDegrees))
                        {
                            Me.CurrentTarget.Face();
                            return result;
                            // StyxWoW.SleepForLagDuration();
                            //return RunStatus.Success;
                        }

                        return RunStatus.Failure;
                    }));
        }

        #endregion 

    }
}
