using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class Balance
    {
        private static string _oldDps = "Wrath";

        private static int StarfallRange { get { return TalentManager.HasGlyph("Focus") ? 20 : 40; } }

        private static int CurrentEclipse { get { return BitConverter.ToInt32(BitConverter.GetBytes(StyxWoW.Me.CurrentEclipse), 0); } }

        private static string BoomkinDpsSpell
        {
            get
            {
                if (StyxWoW.Me.HasAura("Eclipse (Solar)"))
                {
                    _oldDps = "Wrath";
                }
                // This doesn't seem to register for whatever reason.
                else if (StyxWoW.Me.HasAura("Eclipse (Lunar)")) //Eclipse (Lunar) => 48518
                {
                    _oldDps = "Starfire";
                }

                return _oldDps;
            }
        }

        static int MushroomCount
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.FactionId == 4 && o.Distance <= 40).Count(o => o.CreatedByUnitGuid == StyxWoW.Me.Guid); }
        }

        static WoWUnit BestAoeTarget
        {
            get { return Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f); }
        }

        static int RadialAoeCount
        {
            get { return Clusters.GetClusterCount(BestAoeTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f); }
        }

        [Class(WoWClass.Druid)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Spec(TalentSpec.BalanceDruid)]
        public static Composite CreateBalanceDruidCombat()
        {
            Common.WantedDruidForm = ShapeshiftForm.Moonkin;
            return new PrioritySelector(
                Spell.WaitForCast(true),
                //Heals, will not heal if in a party or if disabled via setting
                CreateBoomkinHeals(),


                //Inervate
                Spell.Buff("Innervate", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),

                Spell.BuffSelf("Moonkin Form"),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),


                // Ensure we do /petattack if we have treants up.
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Starfall", ret => StyxWoW.Me, ret => SingularSettings.Instance.Druid.UseStarfall && StyxWoW.Me.HasAura("Eclipse (Lunar)")),
                Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.HasAura("Eclipse (Solar)")),

                new Decorator(
                    ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) < 10f * 10f) > 2,
                    new PrioritySelector(
                // If we got 3 shrooms out. Pop 'em
                        //Spell.Cast("Wild Mushroom: Detonate", ret => MushroomCount == 3),

                        //// If Detonate is coming off CD, make sure we drop some more shrooms. 3 seconds is probably a little late, but good enough.
                        //Spell.CastOnGround(
                        //    "Wild Mushroom", ret => BestAoeTarget.Location,
                        //    ret => RadialAoeCount > 2 && Spell.GetSpellCooldown("Wild Mushroom: Detonate").TotalSeconds < 3),

                        // Spread MF/IS
                        Spell.Cast(
                            "Moonfire", ret => Unit.NearbyUnfriendlyUnits.First(u => !u.HasMyAura("Moonfire") && !u.HasMyAura("Sunfire")),
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => !u.HasMyAura("Moonfire") && !u.HasMyAura("Sunfire")) != 0),
                        Spell.Cast(
                            "Insect Swarm", ret => Unit.NearbyUnfriendlyUnits.First(u => !u.HasMyAura("Insect Swarm")),
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => !u.HasMyAura("Insect Swarm")) != 0)
                        )),

                Spell.Cast("Solar Beam", ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast),

                // Starsurge on every proc. Plain and simple.
                Spell.Cast("Starsurge"),

                // Refresh MF/SF
                Spell.Cast(
                    "Moonfire", ret=>false, ret => StyxWoW.Me.CurrentTarget, ret =>
                                                                        (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Moonfire", true).TotalSeconds < 3 &&
                                                                         StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Sunfire", true).Seconds < 3) ||
                                                                        StyxWoW.Me.IsMoving),

                // Make sure we keep IS up. Clip the last tick. (~3s)
                Spell.Cast("Insect Swarm", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Insect Swarm", true).TotalSeconds < 3),

                // And then just spam Wrath/Starfire
                Spell.Cast("Wrath", ret => BoomkinDpsSpell == "Wrath"),
                Spell.Cast("Starfire", ret => BoomkinDpsSpell == "Starfire"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        static Composite CreateBoomkinHeals()
        {
            return new PrioritySelector(Spell.Buff(
                    "Regrowth",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RegrowthBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty
                    && !StyxWoW.Me.HasAura("Regrowth"))),
                Spell.Buff(
                    "Rejuvenation",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.RejuvenationBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty
                    && !StyxWoW.Me.HasAura("Rejuvenation"))),
                Spell.Cast(
                    "Healing Touch",
                    ret => (StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Druid.HealingTouchBalance
                    && !SingularSettings.Instance.Druid.NoHealBalance
                    && !StyxWoW.Me.IsInParty)));
        }
    }
}
