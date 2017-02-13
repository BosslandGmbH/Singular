using System;
using System.Linq;
using System.Numerics;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Singular.Settings;
using Styx.Common;

namespace Singular.ClassSpecific.DeathKnight
{
    public static class Blood
    {
        private static DeathKnightSettings DeathKnightSettings => SingularSettings.Instance.DeathKnight();
        private static LocalPlayer Me => StyxWoW.Me;

        const int CrimsonScourgeProc = 81141;

        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombatBuffs()
        {
            return new Decorator(
                req => !Me.GotTarget() || !Me.CurrentTarget.IsTrivial(),
                new PrioritySelector(

                    // *** Defensive Cooldowns ***
                    // Anti-magic shell - no cost and doesnt trigger GCD
					Common.CreateAntiMagicShellBehavior(),

                    Spell.Cast("Dancing Rune Weapon",
                        ret => Unit.UnfriendlyUnits(10).Count() > 2 || Me.HealthPercent < DeathKnightSettings.DancingRuneWeaponPercent),

                    Spell.BuffSelf("Vampiric Blood",
                        ret => Me.HealthPercent < DeathKnightSettings.VampiricBloodPercent
                            && (!DeathKnightSettings.VampiricBloodExclusive || !Me.HasAnyAura("Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude")))
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.All, 1)]
        public static Composite CreateDeathKnightBloodPull()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Death's Caress")
                    );
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombat()
        {
            TankManager.NeedTankTargeting = (SingularRoutine.CurrentWoWContext == WoWContext.Instances);

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        ctx => TankManager.Instance.TargetList.FirstOrDefault(u => u.IsWithinMeleeRange) ?? Me.CurrentTarget,

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDeathKnightPullMore(),

                        new Decorator(
                            ret => SingularSettings.Instance.EnableTaunting && SingularRoutine.CurrentWoWContext == WoWContext.Instances
                                && TankManager.Instance.NeedToTaunt.Any()
                                && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                            new Throttle(TimeSpan.FromMilliseconds(1500),
                                new PrioritySelector(
                                    // Direct Taunt
                                    Spell.Cast("Dark Command",
                                        ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                        ret => true),

                                    new Decorator(
                                        ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
                                            && Me.SpellDistance(TankManager.Instance.NeedToTaunt.FirstOrDefault()) > 10,

                                        // use DG if we have to (be sure to stop movement)
                                        Common.CreateDeathGripBehavior()
                                    )
                                )
                            )
                        ),

                        Common.CreateDeathGripBehavior(),

                        // Talents
                        Spell.Cast("Blooddrinker", on => (WoWUnit)on, ret => Me.HealthPercent <= DeathKnightSettings.BloodDrinkerPercent),
                        Spell.Buff("Mark of Blood", on => (WoWUnit)on, ret => Me.HealthPercent <= DeathKnightSettings.MarkOfBloodPercent),
                        Spell.Cast("Tombstone", on => (WoWUnit)on, ret => Me.HealthPercent <= DeathKnightSettings.TombstonePercent && Me.GetAuraStacks("Bone Shield") >= DeathKnightSettings.TombstoneBoneShieldCharges),
                        Spell.Cast("Rune Tap", on => (WoWUnit)on, ret => Me.HealthPercent <= DeathKnightSettings.RuneTapPercent),
                        Spell.Cast("Bonestorm", on => (WoWUnit)on, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < 8) >= DeathKnightSettings.BonestormCount && Me.RunicPowerPercent >= DeathKnightSettings.BonestormRunicPowerPercent),

                        // refresh diseases if possible
                        Spell.Cast("Death's Caress", on => (WoWUnit)on,
                            req => Unit.UnfriendlyUnits(10).Any(u => !u.HasMyAura("Blood Plague") && u.Distance.Between(10, 30))),
                        Spell.Cast("Blood Boil", on => (WoWUnit)on,
                            req => Unit.UnfriendlyUnits(10).Any(u => !u.HasMyAura("Blood Plague") && u.Distance < 10)),

                        // Start AoE section
                        new Decorator(
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) > 1,
                            new PrioritySelector(
                                Spell.Cast("Consumption", ret => DeathKnightSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None),
                                Spell.CastOnGround("Death and Decay", loc => {
                                            Vector3 locTar = (loc as WoWUnit).Location;
                                            Vector3 locMe = Me.Location;
                                    float dist = (float)locMe.Distance(locTar) * 3f / 4f;
                                    dist = Math.Min(dist, 25f);
                                    return Styx.Helpers.WoWMathHelper.CalculatePointFrom(locMe, locTar, (float)dist);
                                }, ret => Unit.UnfriendlyUnitsNearTarget(15).Count() >= DeathKnightSettings.DeathAndDecayCount, false),
                                Spell.Cast("Marrowrend", on => (WoWUnit)on, ret => Me.GetAuraStacks("Bone Shield") < (Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) >= 4 ? 5 : 1)),
                                Spell.Cast("Death Strike", on => (WoWUnit)on),
                                Spell.Cast("Heart Strike", on => (WoWUnit)on, ret => Me.GetAuraStacks("Bone Shield") >= (Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) >= 4 ? 5 : 1) && Me.CurrentRunes > 0),
                                Spell.Cast("Blood Boil", on => (WoWUnit)on, ret => Me.CurrentRunes <= 0 && Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) >= DeathKnightSettings.BloodBoilCount)
                                )
                            ),

                        // Single target rotation
                        Spell.Cast("Consumption", ret => DeathKnightSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None && !DeathKnightSettings.UseArtifactOnlyInAoE),
                        Spell.CastOnGround("Death and Decay", loc => {
                                            Vector3 locTar = (loc as WoWUnit).Location;
                                            Vector3 locMe = Me.Location;
                            float dist = (float)locMe.Distance(locTar) * 3f / 4f;
                            dist = Math.Min(dist, 25f);
                            return Styx.Helpers.WoWMathHelper.CalculatePointFrom(locMe, locTar, (float)dist);
                        }, ret => Me.HasAura(CrimsonScourgeProc)),
                        Spell.Cast("Marrowrend", on => (WoWUnit)on, ret => Me.GetAuraStacks("Bone Shield") < 5),
                        Spell.Cast("Death Strike", on => (WoWUnit)on),
                        new Decorator(ret => Me.GetAuraStacks("Bone Shield") >= 5 && Me.CurrentRunes > 0,
                            new PrioritySelector(
                                Spell.CastOnGround("Death and Decay", loc => {
                                            Vector3 locTar = (loc as WoWUnit).Location;
                                    Vector3 locMe = Me.Location;
                                    float dist = (float)locMe.Distance(locTar) * 3f / 4f;
                                    dist = Math.Min(dist, 25f);
                                    return Styx.Helpers.WoWMathHelper.CalculatePointFrom(locMe, locTar, (float)dist);
                                            }, req => true),
                                Spell.Cast("Heart Strike", on => (WoWUnit)on)
                                )
                            ),
                        Spell.Cast("Blood Boil", on => (WoWUnit)on, req => Unit.UnfriendlyUnits(10).Any())
                        )
                    )
                );
        }

        #endregion

    }
}