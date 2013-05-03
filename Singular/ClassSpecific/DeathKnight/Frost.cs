using System.Collections.Generic;
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

namespace Singular.ClassSpecific.DeathKnight
{
    public class Frost
    {
        private const int KillingMachine = 51124;

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DeathKnightSettings DeathKnightSettings { get { return SingularSettings.Instance.DeathKnight(); } }

        #region Normal Rotations

        private static List<WoWUnit> _nearbyUnfriendlyUnits;

        private static bool IsDualWielding
        {
            get { return Me.Inventory.Equipped.MainHand != null && Me.Inventory.Equipped.OffHand != null; }
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Normal)]
        public static Composite CreateDeathKnightFrostNormalCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost"),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    // Spell.Cast("Gorefiend's Grasp"),
                                    Spell.Cast("Remorseless Winter"),
                                    CreateFrostAoeBehavior(),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),

                        // *** Dual Weld Single Target Priority
                        new Decorator(ctx => IsDualWielding,
                            new PrioritySelector(
                                // Execute
                                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),
                                // Diseases
                                Common.CreateApplyDiseases(),

                                // Killing Machine
                                Spell.Cast("Frost Strike",
                                            ret =>
                                            !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                            Me.HasAura(KillingMachine)),
                                Spell.Cast("Obliterate",
                                            ret =>
                                            Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),
                                // RP Capped
                                Spell.Cast("Frost Strike",
                                            ret =>
                                            !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                            NeedToDumpRunicPower ),
                                // Rime Proc
                                Spell.Cast( sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),

                                // both Unholy Runes are off cooldown
                                Spell.Cast("Obliterate", ret => Me.UnholyRuneCount == 2),
                                Spell.Cast("Frost Strike"),
                                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),
                                Spell.Cast("Horn of Winter")
                                )
                            ),

                        // *** 2 Hand Single Target Priority
                        new Decorator(ctx => !IsDualWielding,
                                      new PrioritySelector(
                                          // Execute
                                          Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                          // Diseases
                                          Common.CreateApplyDiseases(),

                                          // Killing Machine
                                          Spell.Cast("Obliterate", ret => Me.HasAura(KillingMachine)),

                                          // RP Capped
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     NeedToDumpRunicPower ),
                                          // Rime Proc
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),
                                          Spell.Cast("Obliterate"),
                                          Spell.Cast("Frost Strike"),
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),
                                          Spell.Cast("Horn of Winter")
                                          )),

                        // *** 3 Lowbie Cast what we have Priority
                        new Decorator(
                            ret => !SpellManager.HasSpell("Obliterate"),
                            new PrioritySelector(
                                Spell.Buff(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", true, on => Me.CurrentTarget, req => true, "Frost Fever"),
                                Spell.Buff("Plague Strike", true, on => Me.CurrentTarget, req => true, "Blood Plague"),
                                Spell.Cast("Death Strike", ret => Me.HealthPercent < 90),
                                Spell.Cast("Frost Strike"),
                                Spell.Cast("Death Coil", ret => !Me.CurrentTarget.IsWithinMeleeRange ),
                                Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),
                                Spell.Cast("Plague Strike")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightFrostPvPCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateDarkSuccorBehavior(),

                        Common.CreateGetOverHereBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost"),
                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.Cast("Gorefiend's Grasp"),
                                    Spell.Cast("Remorseless Winter"),
                                    Spell.Cast("Necrotic Strike", ret => Me.CurrentTarget.HasAuraExpired("Necrotic Strike", 1)),
                                    CreateFrostAoeBehavior(),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),
                        // *** Dual Weld Single Target Priority
                        new Decorator(ctx => IsDualWielding,
                                      new PrioritySelector(
                                          // Execute
                                          Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                          // Diseases
                                          Common.CreateApplyDiseases(),

                                          // Killing Machine
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     Me.HasAura(KillingMachine)),
                                          Spell.Cast("Obliterate",
                                                     ret =>
                                                     Me.HasAura(KillingMachine) && Common.UnholyRuneSlotsActive == 2),
                                          Spell.Cast("Necrotic Strike", ret => Me.CurrentTarget.HasAuraExpired("Necrotic Strike", 1)),

                                          // RP Capped
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     NeedToDumpRunicPower ),
                                          // Rime Proc
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),
                                          // both Unholy Runes are off cooldown
                                          Spell.Cast("Obliterate", ret => Me.UnholyRuneCount == 2),
                                          Spell.Cast("Frost Strike"),
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),
                                          Spell.Cast("Horn of Winter")
                                          )),
                        // *** 2 Hand Single Target Priority
                        new Decorator(ctx => !IsDualWielding,
                                      new PrioritySelector(
                                          // Execute
                                          Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                          // Diseases
                                          Common.CreateApplyDiseases(),

                                          // Killing Machine
                                          Spell.Cast("Obliterate", ret => Me.HasAura(KillingMachine)),
                                          Spell.Cast("Necrotic Strike", ret => Me.CurrentTarget.HasAuraExpired("Necrotic Strike", 1)),

                                          // RP Capped
                                          Spell.Cast("Frost Strike",
                                                     ret =>
                                                     !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) &&
                                                     NeedToDumpRunicPower ),
                                          // Rime Proc
                                          Spell.Cast(sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch", req => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.HasAura("Freezing Fog")),

                                          Spell.Cast("Obliterate"),
                                          Spell.Cast("Frost Strike"),
                                          Spell.Cast("Horn of Winter")
                                          ))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotations

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost, WoWContext.Instances)]
        public static Composite CreateDeathKnightFrostInstanceCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        // Symbiosis
                        Spell.CastOnGround("Wild Mushroom: Plague", ret => StyxWoW.Me.CurrentTarget.Location, ret => Spell.UseAOE, false),

                        // Cooldowns
                        Spell.BuffSelf("Pillar of Frost"),

                        // Start AoE section
                        new PrioritySelector(
                            ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(12f).ToList(),
                            new Decorator(
                                ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= DeathKnightSettings.DeathAndDecayCount,
                                new PrioritySelector(
                                    // Spell.Cast("Gorefiend's Grasp", ret => Group.Tanks.FirstOrDefault()),
                                    CreateFrostAoeBehavior(),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    )
                                )
                            ),

                        // *** Dual Weld Single Target Priority
                        new Decorator(
                            ctx => IsDualWielding,
                            new PrioritySelector(

                                // Execute
                                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                // Killing Machine
                                Spell.Cast("Frost Strike",
                                    ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                                        && (NeedToDumpRunicPower || Me.HasAura(KillingMachine))),

                                // Diseases
                                Common.CreateApplyDiseases(),

                                // DnD
                                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => Spell.UseAOE && Me.UnholyRuneCount >= 2, false),

                                // Howling Blastwith both frost or both death off cooldown
                                Spell.Cast("Howling Blast",
                                    ret => !Spell.UseAOE
                                        && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                                        && (Me.FrostRuneCount == 2 || Me.DeathRuneCount == 2)),

                                // Obliterate when Killing Machine is procced and both diseases are on the target and both Unholy Runes are off cooldown
                                Spell.Cast("Obliterate",
                                    ret => Me.HasAura(KillingMachine)
                                        && Common.UnholyRuneSlotsActive == 2
                                        && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                                        && !Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                                // Howling Blast if Rime procced
                                Spell.Cast("Howling Blast",
                                    ret => !Spell.UseAOE 
                                        && !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                                        && Me.HasAura("Freezing Fog")),

                                // Obliterate when both diseases are on the target and one Unholy Runes is off cooldown
                                Spell.Cast("Obliterate",
                                    ret => Me.HasAura(KillingMachine)
                                        && Common.UnholyRuneSlotsActive == 1
                                        && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                                        && !Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                                // Howling Blast
                                Spell.Cast( sp => Spell.UseAOE ? "Howling Blast" : "Icy Touch"),

                                // Horn of Winter
                                Spell.Cast("Horn of Winter"),

                                // Frost Strike
                                Spell.Cast("Frost Strike")               
                                )
                            ),

                        // *** 2 Hand Single Target Priority
                        new Decorator(
                            ctx => !IsDualWielding,
                            new PrioritySelector(

                                // Soul Reaper when target below 35%
                                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 35),

                                // Obliterate when Killing Machine is procced and both diseases are on the target
                                Spell.Cast("Obliterate",
                                    ret => Me.HasAura(KillingMachine)
                                        && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                                        && !Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                                // Diseases
                                Common.CreateApplyDiseases(),

                                // Obliterate When any runes are capped
                                Spell.Cast("Obliterate",
                                    ret => Me.FrostRuneCount == 2
                                        || Me.BloodRuneCount == 2
                                        || Me.UnholyRuneCount == 2
                                        || Me.DeathRuneCount == 2 ),

                                // Frost Strike if RP capped
                                Spell.Cast("Frost Strike",
                                    ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                                        && NeedToDumpRunicPower ),

                                // Howling Blast if Rime procced
                                Spell.Cast("Howling Blast",
                                ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)
                                    && Me.HasAura("Freezing Fog")),

                                // Frost Strike
                                Spell.Cast("Frost Strike"),

                                // Obliterate
                                Spell.Cast("Obliterate"),

                                // Horn of Winter
                                Spell.Cast("Horn of Winter")
                                )
                            )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion


        // elitist jerks aoe priority (with addition of spreading diseases, which is mentioned but not specified)
        // .. note: only checking for blood plague in a few cases as frost fever 
        // .. should take care of itself with Howling Blast
        private static Composite CreateFrostAoeBehavior()
        {
            return new PrioritySelector(
                Spell.Cast("Remorseless Winter"),
                Spell.Cast("Soul Reaper", on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 35 && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                // aoe aware disease apply - only checking current target because of ability to spread
                Spell.Cast("Unholy Blight", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance < 10 && u.HasAuraExpired("Blood Plague"))),
                Spell.Cast("Howling Blast", ret => Me.CurrentTarget.HasAuraExpired("Frost Fever")),
                Spell.Cast("Outbreak", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague")),   // only care about blood plague for this one
                Spell.Cast("Plague Strike", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague")),

                // spread disease
                Spell.Cast("Blood Boil",
                    ret => TalentManager.IsSelected((int) DeathKnightTalents.RollingBlood)
                        && Unit.UnfriendlyUnitsNearTarget(10).Any(u => u.HasAuraExpired("Blood Plague"))
                        && Unit.UnfriendlyUnitsNearTarget(10).Any(u => !u.HasAuraExpired("Blood Plague"))),

                Spell.Cast("Pestilence",
                    ret => !Me.CurrentTarget.HasAuraExpired("Blood Plague")
                        && Unit.UnfriendlyUnitsNearTarget(10).Any(u => u.HasAuraExpired("Blood Plague"))),

                // damage
                Spell.Cast("Howling Blast", ret => Me.FrostRuneCount >= 2 || Me.DeathRuneCount >= 2),
                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => Me.UnholyRuneCount >= 2, false),
                Spell.Cast("Frost Strike", ret => NeedToDumpRunicPower ),
                Spell.Cast("Obliterate", ret => !IsDualWielding && Me.HasAura(KillingMachine)),
                Spell.Cast("Howling Blast"),
                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => true, false),
                Spell.Cast("Frost Strike"),
                Spell.Cast("Horn of Winter"),
                Spell.Cast("Plague Strike")
                );
        }

        private static bool NeedToDumpRunicPower
        {
            get
            {
                return Me.CurrentRunicPower >= 76;
            }
        }
    }
}