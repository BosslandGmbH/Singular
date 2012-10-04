using System;
using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Singular.Lists;
using Styx.WoWInternals.WoWObjects;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Shaman
{
    public class Enhancement
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } } 

        #region Common

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances | WoWContext.Normal)]
        public static Composite CreateShamanEnhancementPreCombatBuffs()
        {
            return new PrioritySelector(

                Common.CreateShamanImbueMainHandBehavior( Imbue.Windfury, Imbue.Flametongue ),
                Common.CreateShamanImbueOffHandBehavior( Imbue.Flametongue ),

                Spell.BuffSelf("Lightning Shield"),

                new Decorator(ret => Totems.NeedToRecallTotems,
                    new Action(ret => Totems.RecallTotems()))
                );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds )]
        public static Composite CreateShamanEnhancementPvpPreCombatBuffs()
        {
            return new PrioritySelector(

                Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                Common.CreateShamanImbueOffHandBehavior(Imbue.Frostbrand, Imbue.Flametongue),

                Spell.BuffSelf("Lightning Shield"),

                new Decorator(ret => Totems.NeedToRecallTotems,
                    new Action(ret => Totems.RecallTotems()))
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite CreateShamanEnhancementRest()
        {
            return
                new PrioritySelector(
                    new Decorator(
                        ret => !StyxWoW.Me.HasAura("Drink") && !StyxWoW.Me.HasAura("Food"),
                        CreateShamanEnhancementHeal()),
                    Rest.CreateDefaultRestBehaviour(),
                    Spell.Resurrect("Ancestral Spirit")
                    );
        }
        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal|WoWContext.Instances)]
        public static Composite CreateShamanEnhancementHeal()
        {
            Composite healBT =
                    new Decorator(
                        ret => !Group.Healers.Any(h => h.IsAlive && h.Distance < 40),
                        Spell.Heal("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.GetPredictedHealthPercent() <= 60)
                    );

            // group healing logic ONLY if we are configured for it and in an Instance
            if (SingularRoutine.CurrentWoWContext == WoWContext.Instances && SingularSettings.Instance.Shaman.EnhancementHeal)
            {
                healBT =new Decorator(
                            ret => !StyxWoW.Me.GroupInfo.IsInRaid,
                            new PrioritySelector(
                                // Off Heal the party in dungeons if the healer is dead
                                new Decorator(
                                    ret => StyxWoW.Me.CurrentMap.IsDungeon && !Group.Healers.Any(h => h.IsAlive),
                                    Restoration.CreateRestoShamanHealingOnlyBehavior()),

                                healBT
                                )
                            );
            }

            return healBT;

        }

        [Behavior(BehaviorType.Heal, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds )]
        public static Composite CreateShamanEnhancementHealPvp()
        {
            Composite healBT =
                new PrioritySelector(
                    new Decorator( ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5),
                        new PrioritySelector(
                            ctx => Unit.GroupMembers.Where( p => p.IsAlive && p.GetPredictedHealthPercent() < 50 && p.Distance < 40).FirstOrDefault(),
                            Spell.Cast( "Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.GetPredictedHealthPercent() < 75 ),
                            Spell.Cast( "Healing Surge", ret => (WoWPlayer) ret, ret => StyxWoW.Me.GetPredictedHealthPercent() < 50 )
                            )
                        ),

                    new Decorator(
                        ret => !StyxWoW.Me.Combat,
                        Spell.Heal("Healing Surge", ret => StyxWoW.Me, ret => StyxWoW.Me.GetPredictedHealthPercent() <= 80)
                        )
                    );

            return healBT;
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                Common.CreateShamanImbueOffHandBehavior(Imbue.Flametongue),

                Spell.BuffSelf("Lightning Shield"),

                new Decorator(
                    ret => StyxWoW.Me.Level < 20,
                    new PrioritySelector(
                        Spell.Cast("Lightning Bolt"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )),

                Helpers.Common.CreateAutoAttack(true),
                new Decorator( ret => StyxWoW.Me.CurrentTarget.DistanceSqr < 20 * 20,
                    Totems.CreateTotemsNormalBehavior()),
                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                Spell.Cast("Unleash Weapon", 
                    ret => StyxWoW.Me.Inventory.Equipped.OffHand != null 
                        && StyxWoW.Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == 5),
                Spell.Cast("Earth Shock"),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Normal)]
        public static Composite CreateShamanEnhancementNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                Common.CreateShamanImbueOffHandBehavior(Imbue.Flametongue),

                Spell.BuffSelf("Lightning Shield"),
                Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                Spell.BuffSelf("Feral Spirit", ret => 
                    SingularSettings.Instance.Shaman.FeralSpiritCastOn == CastOn.All 
                    || (SingularSettings.Instance.Shaman.FeralSpiritCastOn == CastOn.Bosses && StyxWoW.Me.CurrentTarget.Elite)
                    || (SingularSettings.Instance.Shaman.FeralSpiritCastOn == CastOn.Players && Unit.NearbyUnfriendlyUnits.Any(u => u.IsPlayer && u.Combat && u.IsTargetingMeOrPet))),

                Totems.CreateTotemsNormalBehavior(),

                Spell.BuffSelf("Elemental Mastery",
                    ret => !StyxWoW.Me.HasAnyAura(Common.BloodlustName , "Time Warp", "Ancient Hysteria")),

                Common.CreateShamanInCombatBuffs(true),

                Spell.Cast("Stormstrike"),
                Spell.Buff("Flame Shock", true,
                    ret => (StyxWoW.Me.HasAura("Unleash Flame") || !SpellManager.HasSpell("Unleash Elements")) &&
                           (StyxWoW.Me.CurrentTarget.Elite || (SpellManager.HasSpell("Fire Nova") && Unit.UnfriendlyUnitsNearTarget(10).Count(u => u.IsTargetingMeOrPet) >= 3))),
                Spell.Cast("Earth Shock",
                    ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 3 
                        || !StyxWoW.Me.CurrentTarget.Elite
                        || !SpellManager.HasSpell("Flame Shock")),
                Spell.Cast("Lava Lash",
                    ret => StyxWoW.Me.Inventory.Equipped.OffHand != null &&
                           StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                Spell.BuffSelf("Fire Nova",
                    ret => StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock") &&
                           Unit.NearbyUnfriendlyUnits.Count(u => 
                               u.IsTargetingMeOrPet &&
                               u.Location.DistanceSqr(StyxWoW.Me.CurrentTarget.Location) < 10 * 10) >= 3),
                Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                Spell.Cast("Unleash Elements"),

                new Decorator(ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5),
                    new PrioritySelector(
                        Spell.Cast("Chain Lightning", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                        Spell.Cast("Lightning Bolt")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Battlegrounds)]
        public static Composite CreateShamanEnhancementPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !SpellManager.GlobalCooldown, 
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                        Common.CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                        Common.CreateShamanImbueOffHandBehavior(Imbue.Frostbrand, Imbue.Flametongue),

                        Spell.BuffSelf("Lightning Shield"),
                        Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                        Spell.BuffSelf("Feral Spirit"),

                        Totems.CreateTotemsPvPBehavior(),

                        Spell.Cast("Stormstrike"),
                        Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                        Spell.Cast("Lava Lash", 
                            ret => StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                                   StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),

                        new Decorator(ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5) && (StyxWoW.Me.GetAuraTimeLeft("Maelstom Weapon", true).TotalSeconds < 3000 || StyxWoW.Me.GetPredictedHealthPercent() > 90),
                            new PrioritySelector(
                                Spell.Cast("Chain Lightning", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                                Spell.Cast("Lightning Bolt")
                                )
                            ),

                        Spell.Cast("Unleash Elements"),
                        Spell.Buff("Flame Shock", true, ret => StyxWoW.Me.HasAura("Unleash Wind") || !SpellManager.HasSpell("Unleash Elements")),
                        Spell.Cast("Earth Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 6)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement, WoWContext.Instances)]
        public static Composite CreateShamanEnhancementInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.BuffSelf("Lightning Shield"),
                Spell.BuffSelf("Spiritwalker's Grace", ret => StyxWoW.Me.IsMoving && StyxWoW.Me.Combat),
                Spell.BuffSelf("Feral Spirit"),

                Totems.CreateTotemsInstanceBehavior(),

                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3,
                    new PrioritySelector(
                        Spell.Cast("Unleash Elements"),
                        Spell.Buff("Flame Shock", true),
                        Spell.Cast("Lava Lash", 
                            ret => StyxWoW.Me.CurrentTarget.HasMyAura("Flame Shock") &&
                                   StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                                   StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                        Spell.Cast("Fire Nova"),
                        Spell.Cast("Chain Lightning", ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                        Spell.Cast("Stormstrike"),
                        Movement.CreateMoveToMeleeBehavior(true)
                        )),

                Spell.Cast("Stormstrike"),
                Spell.Cast("Primal Strike", ret => !SpellManager.HasSpell("Stormstrike")),
                Spell.Cast("Lava Lash",
                    ret => StyxWoW.Me.Inventory.Equipped.OffHand != null && 
                           StyxWoW.Me.Inventory.Equipped.OffHand.ItemInfo.ItemClass == WoWItemClass.Weapon),
                Spell.Cast("Lightning Bolt", ret => StyxWoW.Me.HasAura("Maelstrom Weapon", 5)),
                Spell.Cast("Unleash Elements"),
                Spell.Buff("Flame Shock", true, ret => StyxWoW.Me.HasAura("Unleash Wind") || !SpellManager.HasSpell("Unleash Elements")),
                Spell.Cast("Earth Shock", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Flame Shock", true).TotalSeconds > 6),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

    }
}
