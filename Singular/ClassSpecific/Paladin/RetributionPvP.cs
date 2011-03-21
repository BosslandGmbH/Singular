#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: 
// $Date: 
// $HeadURL:
// $LastChangedBy:
// $LastChangedDate:
// $LastChangedRevision:
// $Revision:

#endregion

using System.Linq;

using Styx.Combat.CombatRoutine;
using System.Collections.Generic;

using Singular.Settings;

using TreeSharp;
using Styx.Logic.Combat;
using CommonBehaviors.Actions;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx;
using Singular.Composites;

namespace Singular
{



    partial class SingularRoutine
    {

        private WaitTimer targetAwayFromMeleeTimer = new WaitTimer(System.TimeSpan.FromSeconds(10));
        private ulong targetAwayFromMeleeGuid = 0;
        private bool targetIsNotUnderPreventingDamage = true;
        private bool targetIsNotUnderPreventingStun = true;
        private bool targetIsNotUnderControl = true;

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateRetributionPaladinBattlegroundsCombat()
        {
            return
                new PrioritySelector(
                    new PrioritySelector(
                        new Decorator(
                            ret => CurrentWoWContext != WoWContext.Battlegrounds,
                            CreateEnsureTarget()),
                        new Decorator(
                            ret => CurrentWoWContext == WoWContext.Battlegrounds,
                            CreateEnsurePVPTarget())),

                    // check for crowd control
                    CreateRetryRemoveCC(),
                    // always move to target
                    CreateMoveToAndFace(),
                    CheckTargetUnderPreventingSpell(),
                    // Heal if target is far and we have Divine Purpose
                    CreateSpellCast("Word of Glory",
                        ret => Me.HealthPercent <= 80 &&
                               (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose"))),
                    // Cast Judgement if we are far away, cos it will speed us up             
                    CreateSpellCast("Judgement", ret => Me.CurrentTarget.DistanceSqr > (8 * 8) &&targetIsNotUnderControl),
                   // Cast Hammer of Wrath on target if we can
                    CreateSpellCast("Hammer of Wrath", ret => targetIsNotUnderPreventingDamage),
                    // Cast Exorcism while approaching to target
                    CreateSpellCast("Exorcism",
                        ret => Me.ActiveAuras.ContainsKey("The Art of War") &&
                               Me.CurrentTarget.DistanceSqr > (8 * 8) &&
                               targetIsNotUnderControl &&
                               targetIsNotUnderPreventingDamage),
                    // Cast Repentance if target is far and is casting
                    CreateSpellCast("Repentance",
                        ret => Me.CurrentTarget.DistanceSqr > (12 * 12) &&
                               (Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0) &&
                               targetIsNotUnderControl &&
                               targetIsNotUnderPreventingDamage),
                   // Stun target if it's far
                   CreateSpellCast("Hammer of Justice",
                        ret => Me.CurrentTarget.DistanceSqr > (10 * 10) && 
                               targetIsNotUnderPreventingDamage &&
                               targetIsNotUnderControl),
                    // Put some additional buffs
                    CreateRetributionPaladinCombatBuffs(),
                    CreateAutoAttack(true),
                    // if target is invulnerable to damage, don't do anything..
                    new Decorator(
                        ret => !targetIsNotUnderPreventingDamage,
                        new Action(ret => { return RunStatus.Success; })),
                    // Break target's casting
                    CreateSpellCast("Rebuke", ret => (Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0)),
                    CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower == 3 || Me.HasAura("Divine Purpose")),
                    CreateSpellCast("Exorcism",
                        ret => Me.ActiveAuras.ContainsKey("The Art of War")),
                    CreateSpellCast("Templar's Verdict", 
                        ret => Me.CurrentHolyPower == 3),
                    CreateSpellCast("Crusader Strike"),
                    CreateSpellCast("Templar's Verdict", 
                        ret => Me.HasAura("Divine Purpose")),
                    CreateSpellCast("Judgement"),
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateSpellCast("Hammer of Justice", ret => targetIsNotUnderPreventingStun),
                    CreateSpellCast("Holy Wrath")
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public Composite CreateRetributionPaladinBattlegroundsPull()
        {
            return
                new PrioritySelector(
                    // reset initial timer
                    new Action(ret => 
                        {
                            targetAwayFromMeleeTimer.Reset();
                            return RunStatus.Failure;
                        }),
                    new PrioritySelector(
                        new Decorator(
                            ret => CurrentWoWContext == WoWContext.Battlegrounds,
                            CreateEnsurePVPTarget()),
                        new Decorator(
                            ret => CurrentWoWContext != WoWContext.Battlegrounds,
                            CreateEnsureTarget())),
                    CreateSpellCast("Judgement"),
                    // put repentance on a target if it's casting sometthing
                    CreateSpellCast("Repentance", 
                        ret => Me.CurrentTarget.DistanceSqr > 15 * 15 &&
                               (Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0)),
					CreateMoveToAndFace(),
					CreateAutoAttack(true)
                    );
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateRetributionPaladinBattlegroundsCombatBuffs()
        {
            return
                new PrioritySelector(
					CreateSpellBuffOnSelf("Divine Protection", ret => Me.HealthPercent <= 60),
                    CreateSpellCast("Gift of the Naaru", ret => Me.HealthPercent < 70),
                    CreateSpellCast("Holy Radiance", ret => Me.HealthPercent < 60),
                    CreateSpellBuffOnSelf("Divine Plea", ret => Me.HealthPercent > 50 && Me.ManaPercent < 15),

                    // Use Avenging Wrath with some damage trinket in slot 2 if it's usable
                    new Decorator(
                        ret => !Me.HasAura("Zealotry"),
                        new Sequence(
                            CreateSpellBuffOnSelf("Avenging Wrath"),
                            new Decorator(
                                ret => StyxWoW.Me.Inventory.Equipped.Trinket2.Cooldown == 0 &&
                                       StyxWoW.Me.Inventory.Equipped.Trinket2.Usable,
                                new Action(ret =>
                                    {
                                        StyxWoW.Me.Inventory.Equipped.Trinket2.Use();
                                        return RunStatus.Success;
                                    })))),
                    CreateSpellBuffOnSelf("Zealotry", ret => !Me.HasAura("Avenging Wrath") && Me.HealthPercent > 80),
                    CreateSpellBuffOnSelf("Guardian of Ancient Kings", ret => !Me.HasAura("Zealotry") && !Me.HasAura("Avenging Wrath") && Me.HealthPercent > 70), 
                    CreateSpellBuffOnSelf("Lay on Hands", ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthRet && !Me.HasAura("Forbearance")),
                    CreateSpellBuffOnSelf("Divine Shield", ret => Me.HealthPercent < 30 && !Me.HasAura("Forbearance"))
                    );
        }


        #region Pre-Combat Buffs

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateRetributionPaladinBattlegroundsPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellCast("Word of Glory",
                        ret => Me.HealthPercent <= 80 &&
                               (Me.CurrentHolyPower >= 2 || 
                               Me.HasAura("Divine Purpose"))),
                    CreatePaladinBuffComposite(),
                    CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower == 3 || Me.HasAura("Divine Purpose")),
                    CreateSpellBuffOnSelf("Divine Plea", ret => Me.HealthPercent > 50 && Me.ManaPercent < 40)
                    );
        }

        #endregion

        
        protected Composite CreateEnsurePVPTarget()
        {
            return
                new PrioritySelector(
                    // check if timer elapsed.. try to find closer target within melee range
                    new Decorator(
                        ret => Me.CurrentTarget != null && targetAwayFromMeleeTimer.IsFinished && 
                            Me.CurrentTarget.Guid == targetAwayFromMeleeGuid,
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
                                    unit.Target();
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
                        ret => Me.CurrentTarget == null || Me.CurrentTarget.DistanceSqr > (35 * 35),
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
                                    unit.Target();
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

        private Composite CreateRetryRemoveCC()
        {
            return
                new PrioritySelector(
                    // If I'm under control, use trinket if we can, if we cannot just stop the tree
                    new Decorator(
                        ret => AmIUnderControl(),
                        new PrioritySelector(
                            new Decorator(
                                ret => StyxWoW.Me.Inventory.Equipped.Trinket1.Cooldown == 0,
                                new Action(ret => 
                                    {
                                        StyxWoW.Me.Inventory.Equipped.Trinket1.Use();
                                        return RunStatus.Success;
                                    })),
                            // return true, so as we are under control, we cannot do anything anyway
                            new ActionAlwaysSucceed())),
                    CreateSpellBuffOnSelf("Hand of Freedom",
                        ret => Me.Auras.Any(
                                   aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted ||
                                           aura.Value.Spell.Mechanic == WoWSpellMechanic.Snared ||
                                           aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed) &&
                               Me.CurrentTarget.Distance > 8),
                    CreateSpellBuffOnSelf("Cleanse",
                        ret => Me.ManaPercent > 20 &&
                               Me.Auras.Any(
                                   aura => aura.Value.Spell.Mechanic == WoWSpellMechanic.Rooted ||
                                           aura.Value.Spell.Mechanic == WoWSpellMechanic.Snared ||
                                           aura.Value.Spell.Mechanic == WoWSpellMechanic.Slowed) &&
                               Me.CurrentTarget.DistanceSqr > 8 * 8)

                    );
        }

        private bool AmIUnderControl()
        {
            // dunno why, but I don't feel like mechanic stuff is working
            return Me.ActiveAuras.Any(
                aura => aura.Key.Equals("Deep Freeze") ||
                        aura.Key.Equals("Polymorph") ||
                        aura.Key.Equals("Throwdown") ||
                        aura.Key.Equals("Intimidating Shout") ||
                        aura.Key.Equals("Psychic Scream") ||
                        aura.Key.Equals("Kidney Shot") ||
                        aura.Key.Equals("Fear") ||
                        aura.Key.Equals("Howl of Terror") ||
                        aura.Key.Equals("Blind") ||
                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Stunned ||
                        aura.Value.Spell.Mechanic == WoWSpellMechanic.Horrified);
        }

        private Composite CheckTargetUnderPreventingSpell()
        {
            return
                new Action(ret =>
                    {
                        targetIsNotUnderControl = !Me.CurrentTarget.ActiveAuras.ContainsKey("Repentance") &&
                                !Me.CurrentTarget.ActiveAuras.ContainsKey("Polymorph");
                        targetIsNotUnderPreventingDamage = !Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield") &&
                               !Me.CurrentTarget.ActiveAuras.ContainsKey("Cyclone");
                        targetIsNotUnderPreventingStun = !Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield") &&
                               !Me.CurrentTarget.ActiveAuras.ContainsKey("Anti-Magic Shell") &&
                               !Me.CurrentTarget.ActiveAuras.ContainsKey("Icebound Fortitude");

                        return RunStatus.Failure;
                    });
        }
    }
}