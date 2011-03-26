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
using Styx.Logic;

namespace Singular
{



    partial class SingularRoutine
    {

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateRetributionPaladinBattlegroundsCombat()
        {
            return
                new PrioritySelector(
                    new Decorator(ret => Battlegrounds.IsInsideBattleground,
                        CreateEnsurePVPTargetMelee()),
                    new Decorator(ret => !Battlegrounds.IsInsideBattleground,
                        CreateEnsureTarget()),
                    CreateCheckPlayerPvPState(),
                    CreateCheckTargetPvPState(),
                    new Decorator(
                        ret => MeUnderStunLikeControl || MeUnderSheepLikeControl || MeUnderFearLikeControl,
                        new PrioritySelector(
                            CreateUseAntiPvPControl(),
                // return true, so as we are under control, we cannot do anything anyway
                            new Action(ret => { return RunStatus.Success; }))),
                // always move to target
                    new Decorator(ret => Me.CurrentTarget == null || !Me.CurrentTarget.IsAlive,
                        new Action(ret => { return RunStatus.Success; })),
                    CreateMoveToAndFacePvP(),
                    CreateSpellCast("Hammer of Wrath",
                        ret => !TargetState.Invulnerable &&
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.ReflectMagic),
                    CreateSpellCast("Word of Glory",
                        ret => Me.HealthPercent <= 80 &&
                               (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose"))),
                // Cast Judgement if we are far away, cos it will speed us up             
                    CreateSpellCast("Judgement", ret => Me.CurrentTarget.DistanceSqr > (8 * 8) && !TargetState.Incapacitated),
                // Cast Hammer of Wrath on target if we can
                    CreateSpellBuffOnSelf("Hand of Freedom",
                        ret => (MeSlowed || MeRooted) &&
                               Me.CurrentTarget.DistanceSqr > 8 * 8),
                    CreateSpellCastOnSelf("Cleanse",
                        ret => Me.ManaPercent > 20 && (MeSlowed || MeRooted) &&
                               !Me.HasAura("Hand of Freedom") &&
                               Me.CurrentTarget.DistanceSqr > 8 * 8),
                // Cast Exorcism while approaching to target
                    CreateSpellCast("Exorcism",
                        ret => Me.ActiveAuras.ContainsKey("The Art of War") &&
                               Me.CurrentTarget.DistanceSqr > (8 * 8) &&
                               !TargetState.Invulnerable &&
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.ReflectMagic &&
                               !TargetState.Incapacitated),
                // Cast Repentance if target is far and is casting
                    CreateSpellCast("Repentance",
                        ret => Me.CurrentTarget.DistanceSqr > (15 * 15) &&
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.Rooted &&
                               !TargetState.Invulnerable &&
                               !TargetState.Stunned &&
                               !TargetState.Incapacitated &&
                               !TargetState.Feared),
                // Stun target if it's far
                   CreateSpellCast("Hammer of Justice",
                        ret => Me.CurrentTarget.DistanceSqr > (10 * 10) &&
                               !TargetState.Invulnerable &&
                               !TargetState.Incapacitated &&
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.ResistsStun &&
                               !TargetState.ReflectMagic &&
                               !TargetState.Feared),
                // Put some additional buffs
                    CreateRetributionPaladinBattlegroundsCombatBuffs(),
                    CreateAutoAttack(true),
                // if target is invulnerable to damage, don't do anything..
                // probably later try to find new target
                    new Decorator(
                        ret => Me.CurrentTarget != null && TargetState.Invulnerable,
                        new Action(ret => { return RunStatus.Success; })),
                // Break target's casting
                    CreateSpellCast("Rebuke",
                        ret => (Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0) &&
                               !TargetState.Invulnerable),
                    CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower == 3 || Me.HasAura("Divine Purpose")),
                    CreateSpellCast("Exorcism",
                        ret => Me.ActiveAuras.ContainsKey("The Art of War") &&
                        !TargetState.Invulnerable &&
                        !TargetState.ReflectMagic &&
                        !TargetState.ResistsBinarySpells),
                    CreateSpellCast("Templar's Verdict",
                        ret => Me.CurrentHolyPower == 3),
                    CreateSpellCast("Crusader Strike"),
                    CreateSpellCast("Templar's Verdict",
                        ret => Me.HasAura("Divine Purpose")),
                    CreateSpellCast("Judgement"),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateSpellCast("Hammer of Justice",
                        ret => !TargetState.ResistsBinarySpells &&
                               !TargetState.ResistsStun &&
                               !TargetState.ReflectMagic &&
                               !TargetState.Feared),
                    CreateSpellCast("Holy Wrath"));
        }

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Battlegrounds)]
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
                    new Decorator(ret => Battlegrounds.IsInsideBattleground,
                        CreateEnsurePVPTargetMelee()),
                    new Decorator(ret => !Battlegrounds.IsInsideBattleground,
                        CreateEnsureTarget()),
                    CreateCheckTargetPvPState(),
                    CreateMoveToAndFacePvP(),
                    // Heal if target is far and we have Divine Purpose
                    CreateSpellCast("Word of Glory",
                        ret => Me.HealthPercent <= 80 &&
                               (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose"))),
                   // Cast Hammer of Wrath on target if we can
                    CreateSpellCast("Hammer of Wrath", 
                        ret => !TargetState.Invulnerable && 
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.ReflectMagic),
                // Cast Judgement if we are far away, cos it will speed us up             
                    CreateSpellCast("Judgement", ret => Me.CurrentTarget.DistanceSqr > (8 * 8) && !TargetState.Incapacitated),
                // put repentance on a target if it's casting sometthing
                    CreateSpellCast("Repentance", 
                        ret => Me.CurrentTarget.DistanceSqr > 15 * 15 &&
                               (Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0) &&
                               !TargetState.Invulnerable &&
                               !TargetState.ResistsBinarySpells &&
                               !TargetState.ReflectMagic),
					CreateAutoAttack(true));
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
                            CreateUsePvPDamageIncreaseAbility(),
                            new Action(ret => { return RunStatus.Success; }))),
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

    }
}