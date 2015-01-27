using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot;
using System;
using System.Drawing;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Unholy
    {
        private const int SuddenDoom = 81340;
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Normal)]
        public static Composite CreateDeathKnightUnholyNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),


                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateDeathKnightPullMore(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        Common.CreateDarkSimulacrumBehavior(),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                            req => Me.CurrentTarget.IsWithinMeleeRange 
                                && !PartyBuff.WeHaveBloodlust
                                && Helpers.Common.UseLongCoolDownAbility),

                        Spell.Cast("Summon Gargoyle", req => DeathKnightSettings.UseSummonGargoyle && Helpers.Common.UseLongCoolDownAbility),

                        // SingularRoutine.DetectFightingNearMe(TalentManager.HasGlyph("Blood Boil") ? 15 : 10),
                        // SingularRoutine.DetectFightingNearTarget(TalentManager.HasGlyph("Blood Boil") ? 15 : 10),

                        // aoe
                        new Decorator(
                            req => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(12f).Count() >= DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                                // Spell.Cast("Gorefiend's Grasp"),
                                Spell.Cast("Remorseless Winter"),
                                CreateUnholyAoeBehavior(),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        // Single target rotation.

                        // Target < 45%, Soul Reaper
                        Spell.Cast("Soul Reaper", req => Me.CurrentTarget.HealthPercent < 45),

                        // Diseases
                        Common.CreateApplyDiseases(),

                        // Dark Transformation
                        Spell.Cast("Dark Transformation",
                            req => Me.GotAlivePet
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                && Me.HasAura("Shadow Infusion", 5)),

                        // Scourge Strike/Death and Decay*(Unholy/Death Runes are Capped)
                        new Decorator(
                            req => Common.UnholyRuneSlotsActive >= 2,
                            new PrioritySelector(
                                Spell.CastOnGround("Death and Decay",
                                    on => Me.CurrentTarget,
                                    req => Spell.UseAOE,
                                    false),
                                Spell.Cast("Scourge Strike")
                                )
                            ),

                        // Death Coil (Sudden Doom, high RP)
                        Spell.Cast("Death Coil", req => Me.HasAura(SuddenDoom) || Me.CurrentRunicPower >= 80),                        

                        // Festering Strike (BB and FF are up)
                        Spell.Cast("Festering Strike", req => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                        
                        // Scourge Strike
                        Spell.Cast("Scourge Strike"),
                        
                        // Festering Strike
                        Spell.Cast("Festering Strike"),

                        // post Single target
                        // attack at range if possible
                        Spell.Cast("Death Coil", req => Me.GotTarget() && !Me.CurrentTarget.IsWithinMeleeRange ),

                        // attack with other abilities if we don't know scourge strike yet
                        new Decorator(
                            req => !SpellManager.HasSpell("Scourge Strike"),
                            new PrioritySelector(
                                Spell.Buff("Icy Touch", true, on => Me.CurrentTarget, req => true, "Frost Fever"),
                                Spell.Buff("Plague Strike", true, on => Me.CurrentTarget, req => true, "Blood Plague"),
                                Spell.Cast("Death Strike", req => Me.HealthPercent < 90),
                                Spell.Cast("Death Coil"),
                                Spell.Cast("Icy Touch"),
                                Spell.Cast("Plague Strike")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Composite CreateUnholyAoeBehavior()
        {
            return new PrioritySelector(
                // Spell.Cast("Gorefiend's Grasp", on => Me, req => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance.Between(10,20) && u.IsTargetingMeOrPet ),
                Spell.Cast("Remorseless Winter"),

            // Diseases
                Common.CreateApplyDiseases(),

                Spell.Cast("Dark Transformation",
                    req => Me.GotAlivePet
                        && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                        && Me.HasAura("Shadow Infusion", 5)),

            // spread the disease around.
                new Throttle( TimeSpan.FromSeconds(1.5f),
                    Spell.Cast("Blood Boil",
                        req => Me.CurrentTarget.DistanceSqr <= 10 * 10
                            && !Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases
                        )
                    ),

                Spell.CastOnGround(
                    "Death and Decay",
                    on => Me.CurrentTarget,
                    req => Spell.UseAOE && Common.UnholyRuneSlotsActive >= 2,
                    false
                    ),

                Spell.Cast("Blood Boil",
                    req => Me.CurrentTarget.DistanceSqr <= 10 * 10 && Me.DeathRuneCount > 0 
                        || (Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2)
                        ),

                Spell.Cast("Soul Reaper", req => Me.CurrentTarget.HealthPercent < 35),

                Spell.Cast("Scourge Strike", req => Me.UnholyRuneCount == 2),

                Spell.Cast("Death Coil",
                    req => Me.HasAura(SuddenDoom) 
                        || Me.RunicPowerPercent >= 80 
                        || !Me.GotAlivePet 
                        || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation"))
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightUnholyPvPCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Movement.CreateMoveToMeleeTightBehavior(true),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateSoulReaperHasteBuffBehavior(),

                        Common.CreateDarkSimulacrumBehavior(),

                        // *** Cool downs ***
                        Spell.BuffSelf("Unholy Frenzy",
                            ret => Me.CurrentTarget.IsWithinMeleeRange 
                                && !PartyBuff.WeHaveBloodlust 
                                && Helpers.Common.UseLongCoolDownAbility
                                ),

                        Spell.Cast("Summon Gargoyle", ret => DeathKnightSettings.UseSummonGargoyle && Helpers.Common.UseLongCoolDownAbility),


                        // *** Single target rotation. ***
                        // Execute
                        Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                        // Diseases
                        Spell.Cast("Outbreak",
                            ret => !Me.CurrentTarget.HasMyAura("Frost Fever") 
                                || !Me.CurrentTarget.HasAura("Blood Plague")
                                ),

                        Spell.Buff("Icy Touch", true, ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),

                        Spell.Buff("Plague Strike", true, on => Me.CurrentTarget, req => true, "Blood Plague"),

                        Spell.Cast("Dark Transformation",
                            ret => Me.GotAlivePet 
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") 
                                && Me.HasAura("Shadow Infusion", 5)
                                ),
                        Spell.CastOnGround(
                            "Death and Decay",
                            on => Me.CurrentTarget,
                            req => Spell.UseAOE && Common.UnholyRuneSlotsActive >= 2, 
                            false
                            ),
                        Spell.Cast("Scourge Strike", ret => Me.UnholyRuneCount == 2 || Me.DeathRuneCount > 0),
                        Spell.Cast("Festering Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                        Spell.Cast("Death Coil", ret => Me.HasAura(SuddenDoom) || Me.CurrentRunicPower >= 80),
                        Spell.Cast("Scourge Strike"),
                        Spell.Cast("Festering Strike"),
                        Spell.Cast("Death Coil", ret => !Me.GotAlivePet || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation"))
                        )
                    )
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.Instances)]
        public static Composite CreateDeathKnightUnholyInstanceCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        // *** Cool downs ***
                        Spell.Cast(
                            "Summon Gargoyle",
                            ret => DeathKnightSettings.UseSummonGargoyle 
                                && Helpers.Common.UseLongCoolDownAbility),

                        // Start AoE section
                        new Decorator(
                            ret => Spell.UseAOE 
                                && Unit.UnfriendlyUnitsNearTarget(12f).Count() >= DeathKnightSettings.DeathAndDecayCount,
                            new PrioritySelector(
                                // Diseases
                                Common.CreateApplyDiseases(),

                                // spread the disease around.
                                new Throttle( 2, 
                                    Spell.Cast("Blood Boil",
                                        ret => !Me.HasAura("Unholy Blight") 
                                            && Me.CurrentTarget.DistanceSqr <= 10*10 
                                            && Common.ShouldSpreadDiseases)
                                    ),

                                Spell.Cast("Dark Transformation",
                                    ret => Me.GotAlivePet 
                                        && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") 
                                        && Me.HasAura("Shadow Infusion", 5) ),

                                Spell.CastOnGround("Death and Decay",
                                    loc => Me.CurrentTarget,
                                    req => Common.UnholyRuneSlotsActive >= 2, 
                                    false),

                                Spell.Cast("Blood Boil",
                                    ret => Me.CurrentTarget.DistanceSqr <= 10*10 
                                        && (Me.DeathRuneCount > 0  || (Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2))),

                                // Execute
                                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),
                                Spell.Cast("Scourge Strike", ret => Me.UnholyRuneCount == 2),
                                Spell.Cast("Death Coil",
                                    req => Me.HasAura(SuddenDoom) 
                                        || Me.RunicPowerPercent >= 80 
                                        || !Me.GotAlivePet 
                                        || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")),

                                Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemorselessWinter)),

                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),
                
                        // *** Single target rotation. ***

                        // Single target rotation.

                        // Target < 45%, Soul Reaper
                        Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 45),

                        // Defile Ground around target
                        Spell.CastOnGround("Defile", on => Me.CurrentTarget, req => Spell.UseAOE && Me.GotTarget() && !Me.CurrentTarget.IsMoving, false),

                        // Diseases
                        Common.CreateApplyDiseases(),

                        // Dark Transformation
                        Spell.Cast("Dark Transformation",
                            ret => Me.GotAlivePet
                                && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                && Me.HasAura("Shadow Infusion", 5)),

                        // Death Coil if Dark Trans not active
                        Spell.Cast("Death Coil",
                            req => !Me.GotAlivePet
                                || !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation")
                                || Me.RunicPowerPercent > 90
                            ),

                        // Scourge Strike on Death or Unholy Runes active
                        Spell.Cast("Scourge Strike",
                            req => Common.DeathRuneSlotsActive > 0 
                                || Common.UnholyRuneSlotsActive > 0),

                        // Festering Strike on both Blood and Frost runes active
                        Spell.Cast("Festering Strike",
                            req => Common.BloodRuneSlotsActive > 0
                                && Common.FrostRuneSlotsActive > 0),

                        // Death Coil (Sudden Doom, high RP)
                        Spell.Cast("Death Coil",
                            ret => Me.HasAura(SuddenDoom)
                                || !Me.CurrentTarget.IsWithinMeleeRange
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Diagnostics

        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.All, 99)]
        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy, WoWContext.All, 99)]
        public static Composite CreateDeathKnightUnholyDiagnostic()
        {
            return CreateCombatDiagnosticOutputBehavior( 
                Dynamics.CompositeBuilder.CurrentBehaviorType == BehaviorType.Pull ? "PULL" : "COMBAT"
                );
        }

       private static Composite CreateCombatDiagnosticOutputBehavior(string sState = "")
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new Decorator(
                req => !Spell.IsGlobalCooldown(),
                new ThrottlePasses( TimeSpan.FromMilliseconds(900),
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... [{0}] h={1:F1}%, e={2:F1}%, runes(b{3} f{4} u{5} d{6}), moving={7}, how={8}",
                            sState,
                            Me.HealthPercent,
                            Me.RunicPowerPercent,
                            Me.BloodRuneCount,
                            Me.FrostRuneCount,
                            Me.UnholyRuneCount,
                            Me.DeathRuneCount,
                            Me.IsMoving,
                            (int)Me.GetAuraTimeLeft("Horn of Winter", true).TotalSeconds
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, dies {2}, {3:F1} yds, inmelee={4}, loss={5}, shdwinfus={6}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.TimeToDeath(),
                                target.Distance,
                                target.IsWithinMeleeRange.ToYN(),
                                target.InLineOfSpellSight,
                                Me.GetAuraStacks("Shadow Infusion")
                                );
                        }

                        if (!Me.GotAlivePet)
                            sMsg += ", no pet";
                        else
                            sMsg += string.Format(", peth={0:F1}%, drktrns={1}", 
                                Me.Pet.HealthPercent,
                                Me.Pet.ActiveAuras.ContainsKey("Dark Transformation").ToYN()
                                );

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        #endregion
    }
}