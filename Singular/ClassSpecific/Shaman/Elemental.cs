using System.Linq;

using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Rest = Singular.Helpers.Rest;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Singular.Managers;
using Styx.Common;


namespace Singular.ClassSpecific.Shaman
{
    public class Elemental
    {
        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings ShamanSettings { get { return SingularSettings.Instance.Shaman; } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateShamanElementalPreCombatBuffs()
        {
            return new PrioritySelector(
                Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue),

                Spell.BuffSelf("Lightning Shield"),

                new Decorator(ret => Totems.NeedToRecallTotems,
                    new Action(ret => Totems.RecallTotems()))
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CreateShamanElementalRest()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                        CreateShamanElementalHeal()),
                    Rest.CreateDefaultRestBehaviour(),
                    Spell.Resurrect("Ancestral Spirit")
                    );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal | WoWContext.Instances)]
        public static Composite CreateShamanElementalHeal()
        {
            Composite healBT =
                new Decorator(
                    ret => !StyxWoW.Me.Combat || (Group.Healers.Any() && !Group.Healers.Any(h => h.IsAlive)),
                    Common.CreateShamanNonHealBehavior()
                    );

            // only include group healing logic if we are configured for group heal and in an Instance
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances && SingularSettings.Instance.Shaman.ElementalHeal )
            {
                healBT =new Decorator(
                            ret => !StyxWoW.Me.GroupInfo.IsInRaid,
                            new PrioritySelector(
                                // Heal the party in dungeons if the healer is dead
                                new Decorator(
                                    ret => StyxWoW.Me.CurrentMap.IsDungeon && Group.Healers.Count(h => h.IsAlive) == 0,
                                    Restoration.CreateRestoShamanHealingOnlyBehavior()),

                                healBT
                                )
                            );
            }

            return healBT;
        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Battlegrounds )]
        public static Composite CreateShamanElementalPvPHeal()
        {
            return Common.CreateShamanNonHealBehavior();
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal)]
        public static Composite CreateShamanElementalNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                Spell.BuffSelf("Lightning Shield"),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 40 * 40,
                    Totems.CreateTotemsNormalBehavior()),

                // grinding or questing, if target meets these cast Flame Shock if possible
                // 1. mob is less than 12 yds, so no benefit from delay in Lightning Bolt missile arrival
                // 2. area has another player competing for mobs (we want to tag the mob quickly)
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 12
                        || ObjectManager.GetObjectsOfType<WoWPlayer>(true, false).Any(p => p.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) <= 40 * 40),
                    new PrioritySelector(
                        Spell.Buff("Flame Shock", true),
                        Spell.Cast("Unleash Weapon", ret => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),
                        Spell.Cast("Earth Shock", ret => !SpellManager.HasSpell("Flame Shock"))
                        )
                    ),

                // have a big attack loaded up, so don't waste it
                Spell.Cast("Earth Shock",
                    ret => StyxWoW.Me.HasAura("Lightning Shield", 5)),

                // otherwise, start with Lightning Bolt so we can follow with an instant
                // to maximize damage at initial aggro
                Spell.Cast("Lightning Bolt", ret => !StyxWoW.Me.IsMoving || StyxWoW.Me.HasAura("Spiritwalker's Grace") || TalentManager.HasGlyph("Unleashed Lightning")),

                // we are moving so throw an instant of some type
                Spell.Cast("Flame Shock"),
                Spell.Cast("Unleash Weapon", ret => Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Normal)]
        public static Composite CreateShamanElementalNormalCombat()
        {
            return new PrioritySelector(

                new ThrottlePasses( 1, CreateElementalDiagnosticOutputBehavior()),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Common.InGCD,
                    new PrioritySelector(
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        new Decorator( 
                            ret => Common.GetImbue( StyxWoW.Me.Inventory.Equipped.MainHand) == Imbue.None,
                            Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue)),
                    
                        Spell.BuffSelf("Lightning Shield"),

                        Spell.BuffSelf("Elemental Mastery",
                            ret => Unit.NearbyUnitsInCombatWithMe.Any(u => u.Elite || u.IsPlayer) &&
                                !StyxWoW.Me.HasAnyAura(Common.BloodlustName , "Time Warp", "Ancient Hysteria")),

                        Common.CreateShamanInCombatBuffs(true),

                        Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving),

                        Spell.BuffSelf("Thunderstorm", ret => Unit.NearbyUnfriendlyUnits.Count( u => u.Distance < 10f ) >= 3),

                        Totems.CreateTotemsNormalBehavior(),

                        new Decorator(
                            ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                new Action( act => { Logger.WriteDebug("performing aoe behavior"); return RunStatus.Failure; }),

                                Spell.BuffSelf("Astral Shift", ret => StyxWoW.Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMe.Count() >= 5),

                                Spell.BuffSelf( Common.BloodlustName , 
                                    ret => Unit.NearbyUnitsInCombatWithMe.Count() >= 5 ||
                                        Unit.NearbyUnitsInCombatWithMe.Any( u => u.Elite || u.IsPlayer )),

                                Spell.BuffSelf("Elemental Mastery", ret =>
                                    !StyxWoW.Me.HasAnyAura(Common.BloodlustName , "Time Warp", "Ancient Hysteria")),

                                Spell.CastOnGround("Earthquake", ret => StyxWoW.Me.CurrentTarget.Location, req => 
                                    (StyxWoW.Me.ManaPercent > 60 || StyxWoW.Me.HasAura( "Clearcasting")) &&
                                    Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 6),

                                Spell.Cast("Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(15f), ClusterType.Chained, 12))
                                )),

                        Spell.Buff("Flame Shock", true),

                        Spell.Cast("Lava Burst"),
                        Spell.Cast("Earth Shock",
                            ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),

                        Spell.Cast("Unleash Elements", ret => 
                            StyxWoW.Me.IsMoving &&
                            !StyxWoW.Me.HasAura( "Spiritwalker's Grace") &&
                            Common.IsImbuedForDPS( StyxWoW.Me.Inventory.Equipped.MainHand)),

                        Spell.Cast("Chain Lightning", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                        Spell.Cast("Lightning Bolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Battlegrounds)]
        public static Composite CreateShamanElementalPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                new Decorator( 
                    ret => !Common.InGCD,
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        new Decorator(
                            ret => Common.GetImbue(StyxWoW.Me.Inventory.Equipped.MainHand) == Imbue.None,
                            Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue)),

                        Spell.BuffSelf("Lightning Shield"),

                        Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),

                        Spell.BuffSelf("Elemental Mastery",
                            ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat &&
                                   (!SpellManager.HasSpell("Spiritwalker's Grace") ||
                                   SpellManager.Spells["Spiritwalker's Grace"].Cooldown && !StyxWoW.Me.HasAura("Spiritwalker's Grace"))),

                        Spell.BuffSelf("Thunderstorm", ret => StyxWoW.Me.IsStunned() && Unit.NearbyUnfriendlyUnits.Any( u => u.Distance < 10f)),

                        Totems.CreateTotemsPvPBehavior(),

                        new Decorator(
                            ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                            new PrioritySelector(
                                // Pop the ele on bosses
                                Spell.BuffSelf("Fire Elemental Totem", ret => !StyxWoW.Me.Totems.Any(t => t.WoWTotem == WoWTotem.FireElemental)),
                                Spell.CastOnGround("Earthquake", ret => StyxWoW.Me.CurrentTarget.Location),
                                Spell.Cast("Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(15f), ClusterType.Chained, 12))
                                )),

                        Spell.Buff("Flame Shock", true),
                        Spell.Cast("Lava Burst"),
                        Spell.Cast("Earth Shock",
                            ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                                   StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),
                        Spell.Cast("Unleash Elements",
                            ret => StyxWoW.Me.IsMoving && !StyxWoW.Me.HasAura("Spiritwalker's Grace")
                                && Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),
                        Spell.Cast("Chain Lightning", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                        Spell.Cast("Lightning Bolt")
                        )
                    ),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental, WoWContext.Instances)]
        public static Composite CreateShamanElementalInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Flametongue),

                Spell.BuffSelf("Lightning Shield"),
                Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),

                Spell.BuffSelf("Elemental Mastery", ret => StyxWoW.Me.Combat),

                Totems.CreateTotemsInstanceBehavior(),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        new Action(act => { Logger.WriteDebug("performing aoe behavior"); return RunStatus.Failure; }),
                        Spell.CastOnGround("Earthquake", ret => StyxWoW.Me.CurrentTarget.Location),
                        Spell.Cast("Chain Lightning", ret => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(15f), ClusterType.Chained, 12))
                        )),
                
                Spell.Buff("Flame Shock", true),
                Spell.Cast("Lava Burst"),
                Spell.Cast("Earth Shock", 
                    ret => StyxWoW.Me.HasAura("Lightning Shield", 5) &&
                           StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3),
                Spell.Cast("Unleash Elements",
                    ret => StyxWoW.Me.IsMoving 
                        && !StyxWoW.Me.HasAura("Spiritwalker's Grace")
                        && Common.IsImbuedForDPS(StyxWoW.Me.Inventory.Equipped.MainHand)),
                Spell.Cast("Chain Lightning", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                Spell.Cast("Lightning Bolt"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Diagnostics

        private static Composite CreateElementalDiagnosticOutputBehavior()
        {
            return new Decorator(
                ret => SingularSettings.Instance.EnableDebugLogging,
                new Action(ret =>
                {
                    uint lstks = !Me.HasAura("Lightning Shield") ? 0 : Me.ActiveAuras["Lightning Shield"].StackCount;

                    string line = string.Format(".... h={0:F1}%/m={1:F1}%, lstks={2}",
                        Me.HealthPercent,
                        Me.ManaPercent,
                        lstks
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target == null)
                        line += ", target=(null)";
                    else
                        line += string.Format(", target={0}, th={1:F1}%", target.Name, target.HealthPercent);

                    Logging.WriteToFileSync(LogLevel.Diagnostic, line);
                    return RunStatus.Success;
                })
                );
        }

        #endregion
    }
}
