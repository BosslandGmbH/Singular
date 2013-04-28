using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Blood
    {
        private static DeathKnightSettings Settings { get { return SingularSettings.Instance.DeathKnight(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombatBuffs()
        {
            return new PrioritySelector(

                // *** Defensive Cooldowns ***
                // Anti-magic shell - no cost and doesnt trigger GCD 
                Spell.BuffSelf("Anti-Magic Shell",
                    ret => Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid)),

                // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                Spell.CastOnGround("Anti-Magic Zone", 
                    loc => StyxWoW.Me.Location,
                    ret => Common.HasTalent( DeathKnightTalents.AntiMagicZone) 
                        && !StyxWoW.Me.HasAura("Anti-Magic Shell") 
                        && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsCasting || u.ChanneledCastingSpellId != 0) && u.CurrentTargetGuid == StyxWoW.Me.Guid) 
                        && Targeting.Instance.FirstUnit != null 
                        && Targeting.Instance.FirstUnit.IsWithinMeleeRange),

                Spell.Cast("Dancing Rune Weapon",
                    ret => Unit.NearbyUnfriendlyUnits.Count() > 2),

                Spell.BuffSelf("Bone Shield",
                    ret => !Settings.BoneShieldExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude")),

                Spell.BuffSelf("Vampiric Blood",
                    ret => Me.HealthPercent < Settings.VampiricBloodPercent
                        && (!Settings.VampiricBloodExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),

                Spell.BuffSelf("Icebound Fortitude",
                    ret => StyxWoW.Me.HealthPercent < Settings.IceboundFortitudePercent
                        && (!Settings.IceboundFortitudeExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),

                Spell.BuffSelf("Lichborne",ret => StyxWoW.Me.IsCrowdControlled()),

                Spell.BuffSelf("Desecrated Ground", ret => Common.HasTalent( DeathKnightTalents.DesecratedGround) && StyxWoW.Me.IsCrowdControlled()),

                // Symbiosis
                Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < Settings.VampiricBloodPercent),

                // use army of the dead defensively
                Spell.BuffSelf("Army of the Dead",
                    ret => Settings.UseArmyOfTheDead 
                        && SingularRoutine.CurrentWoWContext == WoWContext.Instances 
                        && StyxWoW.Me.HealthPercent < Settings.ArmyOfTheDeadPercent),

                // I need to use Empower Rune Weapon to use Death Strike
                Spell.BuffSelf("Empower Rune Weapon",
                    ret => StyxWoW.Me.HealthPercent < Settings.EmpowerRuneWeaponPercent
                        && !SpellManager.CanCast("Death Strike")),

                new PrioritySelector(
                    ctx => StyxWoW.Me.PartyMembers.FirstOrDefault(u => u.IsDead && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight),
                    Spell.Cast("Raise Ally", ctx => ctx as WoWUnit, req => Settings.UseRaiseAlly)
                    ),

                // *** Offensive Cooldowns ***
                // I am using pet as dps bonus
                Spell.BuffSelf("Raise Dead",
                    ret => Helpers.Common.UseLongCoolDownAbility
                        && Settings.UseGhoulAsDpsCdBlood 
                        && !Common.GhoulMinionIsActive),

                Spell.BuffSelf("Death's Advance",
                    ret => Common.HasTalent( DeathKnightTalents.DeathsAdvance) 
                        && StyxWoW.Me.GotTarget && !SpellManager.CanCast("Death Grip", false) 
                        && StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10),

                Spell.BuffSelf("Blood Tap",
                    ret => StyxWoW.Me.HasAura("Blood Charge", 5)
                        && (Common.BloodRuneSlotsActive == 0 || Common.FrostRuneSlotsActive == 0 || Common.UnholyRuneSlotsActive == 0)),

                Spell.Cast("Plague Leech", ret => Common.CanCastPlagueLeech)
                );
        }

        #endregion

        #region Normal Rotation

        private readonly static WaitTimer DeathStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(5));
        private static List<WoWUnit> _nearbyUnfriendlyUnits;

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Normal)]
        public static Composite CreateDeathKnightBloodNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),

                        Helpers.Common.CreateAutoAttack(true),

                        Common.CreateDarkSuccorBehavior(),

                        Spell.Buff("Chains of Ice",
                            ret => StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                        Common.CreateDeathGripBehavior(),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(15f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= Settings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),

                                    // Spell.Cast("Gorefiend's Grasp", ret => Common.HasTalent( DeathKnightTalents.GorefiendsGrasp)),
                                    Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),

                                    // refresh diseases if possible
                                    new Throttle(2,
                                        new PrioritySelector(
                                            Spell.Cast("Blood Boil", ret => UseBloodBoilForDiseases()),
                                            Spell.Cast("Pestilence", ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases)
                                            )
                                        ),

                                    // Apply Diseases
                                    Common.CreateApplyDiseases(),

                                    // Active Mitigation (5 second rule does not apply)
                                    Spell.Cast("Death Strike"),

                                    // AoE Damage
                                    Spell.Cast("Blood Boil", ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                                    Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                                    Spell.Cast("Rune Strike"),
                                    Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),


                        // refresh diseases if possible
                        new Throttle( 2,
                            new PrioritySelector(
                                Spell.Cast("Blood Boil", ret => UseBloodBoilForDiseases()),
                                Spell.Cast("Pestilence", ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases)
                                )
                            ),

                        Common.CreateApplyDiseases(),

                        // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                        Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                        Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.Cast("Rune Strike"),

                        // Active Mitigation
                        new Sequence(
                            Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                            new Action(ret => DeathStrikeTimer.Reset())
                            ),


                        Spell.Cast("Blood Boil", ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                        Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                        Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Heart Strike") && _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                        Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                        // *** 3 Lowbie Cast what we have Priority
                        // ... not much to do here, just use our Unholy Runes on PS prior to learning DS
                        Spell.Cast("Plague Strike", ret => !SpellManager.HasSpell( "Death Strike"))
                        )
                    ),
  
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightBloodPvPCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(),
                    Helpers.Common.CreateAutoAttack(true),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Helpers.Common.CreateInterruptBehavior(),
                            Common.CreateDeathGripBehavior(),
                            Spell.Buff("Chains of Ice",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                            Common.CreateDarkSuccorBehavior(),

                            // Start AoE section
                            Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),
                            Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),

                            // renew/spread disease if possible
                            Spell.Cast("Blood Boil", ret => Spell.UseAOE && UseBloodBoilForDiseases()),
                            Spell.Cast("Pestilence", ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),


                            // apply / refresh disease if needed 
                            Common.CreateApplyDiseases(),

                            Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),

                            // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                            Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                            Spell.Cast("Rune Strike"),
                            Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange ),
                            Spell.Buff("Necrotic Strike"),
                            Spell.Cast("Death Strike"),
                            Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                            Spell.Cast("Icy Touch"),
                            Spell.Cast("Horn of Winter")
                            )
                        ),

                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        #endregion

        #region Tanking - Instances and Raids

        // Blood DKs no longer pull with DG... now save cooldown for Taunt if possible
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Instances)]
        public static Composite CreateDeathKnightBloodInstancePull()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateDismount("Pulling"),
                    Movement.CreateEnsureMovementStoppedWithinMelee(),
                    Spell.WaitForCast(),

                    Helpers.Common.CreateAutoAttack(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Spell.BuffSelf("Horn of Winter"),
                            Spell.Cast("Outbreak"),
                            Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                            Spell.Cast("Plague Strike"),
                            Spell.Cast("Blood Strike"),
                            Spell.Cast("Death Coil")
                            )
                        ),

                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Instances)]
        public static Composite CreateDeathKnightBloodInstanceCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(),
                    Helpers.Common.CreateAutoAttack(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Helpers.Common.CreateInterruptBehavior(),

                            // Taunts
                            new Decorator( 
                                ret => SingularSettings.Instance.EnableTaunting 
                                    && TankManager.Instance.NeedToTaunt.Any()
                                    && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                                new Throttle( TimeSpan.FromMilliseconds(1500),
                                    new PrioritySelector(
                                        // Direct Taunt
                                        Spell.Cast("Dark Command",
                                            ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                            ret => true),

                                        new Decorator(
                                            ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
                                                && Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,

                                            new PrioritySelector(
                                                // use DG if we have to (be sure to stop movement)
                                                Common.CreateDeathGripBehavior(),

                                                // CoI for the agro and to slow
                                                Spell.Cast("Chains of Ice",
                                                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                                    req => Me.IsSafelyFacing(TankManager.Instance.NeedToTaunt.FirstOrDefault())),

                                                // everything else on CD, so hit with a DC if possible
                                                Spell.Cast("Death Coil", 
                                                    ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(), 
                                                    req => Me.IsSafelyFacing(TankManager.Instance.NeedToTaunt.FirstOrDefault()))
                                                )
                                            )
                                        )
                                    )
                                 ),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(15f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= Settings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),

                                    // Spell.Cast("Gorefiend's Grasp", ret => Common.HasTalent( DeathKnightTalents.GorefiendsGrasp)),
                                    Spell.Cast("Remorseless Winter", ret => Common.HasTalent(DeathKnightTalents.RemoreselessWinter)),

                                    // Apply Diseases
                                    Common.CreateApplyDiseases(),

                                    // Spread Diseases
                                    Spell.Cast("Blood Boil",
                                        ret => Common.HasTalent(DeathKnightTalents.RollingBlood)
                                            && !StyxWoW.Me.HasAura("Unholy Blight")
                                            && StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10
                                            && Common.ShouldSpreadDiseases),

                                    Spell.Cast("Pestilence",
                                        ret => !StyxWoW.Me.HasAura("Unholy Blight")
                                            && Common.ShouldSpreadDiseases),

                                    // Active Mitigation
                                    new Sequence(
                                        Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                        new Action(ret => DeathStrikeTimer.Reset())
                                        ),

                                    // AoE Damage
                                    Spell.Cast("Blood Boil", ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                                    Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                                    Spell.Cast("Rune Strike"),
                                    Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),

                        // refresh diseases if possible and needed
                        Spell.Cast("Blood Boil",
                            ret => Spell.UseAOE
                                && SpellManager.HasSpell("Scarlet Fever")
                                && StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10
                                && Unit.NearbyUnfriendlyUnits.Any(u =>
                                {
                                    long frostTimeLeft = (long)u.GetAuraTimeLeft("Frost Fever").TotalMilliseconds;
                                    long bloodTimeLeft = (long)u.GetAuraTimeLeft("Blood Plauge").TotalMilliseconds;
                                    return frostTimeLeft > 500 && bloodTimeLeft > 500 && (frostTimeLeft < 3000 || bloodTimeLeft < 3000);
                                })
                            ),

                        Common.CreateApplyDiseases(),

                        // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                        Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                        Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.Cast("Rune Strike"),

                        // Active Mitigation - just cast DS on cooldown
                        Spell.Cast("Death Strike"),
                        /*
                        new Sequence(
                            Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                            new Action(ret => DeathStrikeTimer.Reset())
                            ),
                        */

                        Spell.Cast("Blood Boil", ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                        Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                        Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                        Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Heart Strike") && _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                        Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                        // *** 3 Lowbie Cast what we have Priority
                        // ... not much to do here, just use our Unholy Runes on PS prior to learning DS
                        Spell.Cast("Plague Strike", ret => !SpellManager.HasSpell("Death Strike"))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static bool UseBloodBoilForDiseases()
        {
            if ( !Spell.UseAOE)
                return false;

            if ( SpellManager.HasSpell("Scarlet Fever") && NeedsRefresh( Me.CurrentTarget))
                return true;

            if ( !NeedsDisease(Me.CurrentTarget))
            {
                int radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;
                if (Common.HasTalent(DeathKnightTalents.RollingBlood))
                    return Unit.NearbyUnfriendlyUnits.Any( u => u.Guid != Me.CurrentTargetGuid && Me.SpellDistance(u) < radius && NeedsDiseaseOrRefresh(u));
            }

            return false;
        }

        private static bool NeedsDisease( WoWUnit unit)
        {
            return !Me.CurrentTarget.HasAura("Frost Fever") || !Me.CurrentTarget.HasAura("Blood Plague"); 
        }

        private static bool NeedsRefresh( WoWUnit unit)
        {
            long frostTimeLeft = (long)unit.GetAuraTimeLeft("Frost Fever").TotalMilliseconds;
            long bloodTimeLeft = (long)unit.GetAuraTimeLeft("Blood Plauge").TotalMilliseconds;
            return frostTimeLeft > 500 && bloodTimeLeft > 500 && (frostTimeLeft < 3000 || bloodTimeLeft < 3000);
        }

        private static bool NeedsDiseaseOrRefresh( WoWUnit unit)
        {
            long frostTimeLeft = (long)unit.GetAuraTimeLeft("Frost Fever").TotalMilliseconds;
            long bloodTimeLeft = (long)unit.GetAuraTimeLeft("Blood Plauge").TotalMilliseconds;
            return (frostTimeLeft < 3000 || bloodTimeLeft < 3000);
        }

        #endregion
    }
}
