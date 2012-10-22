#region

using System;
using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid; } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        public const WoWSpec DruidAllSpecs = WoWSpec.DruidBalance | WoWSpec.DruidFeral | WoWSpec.DruidGuardian | WoWSpec.DruidRestoration;

        public static List<WoWUnit> EnemyUnits
        {
            get
            {
                return
                    ObjectManager.GetObjectsOfType<WoWUnit>(true, false)
                        .Where(unit =>
                               !unit.IsFriendly
                               && (unit.IsTargetingMeOrPet
                                   || unit.IsTargetingMyPartyMember
                                   || unit.IsTargetingMyRaidMember
                                   || unit.IsPlayer)
                               && !unit.IsNonCombatPet
                               && !unit.IsCritter
                               && unit.DistanceSqr
                               <= 15*15).ToList();
            }
        }

        public static double energy
        {
            get { return Lua.GetReturnVal<int>("return UnitMana(\"player\");", 0); }
        }

        public static double energyregen
        {
            get { return Lua.GetReturnVal<float>("return GetPowerRegen()", 1); }
        }

        public static double energytime_to_max
        {
            get { return (100 - energy)*(1.0/energyregen); }
        }

        #region PreCombat Buffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid)]
        public static Composite CreateDruidPreCombatBuff()
        {
            // Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
            return new PrioritySelector(

                PartyBuff.BuffGroup( 
                    "Mark of the Wild",
                    ret => DruidSettings.BuffRaidWithMotw && !Me.HasAura("Prowl")
                        && (!Me.Combat || DruidSettings.CatRaidRebuff)
                    )

                /*   This logic needs work. 
                new Decorator(
                    ret =>
                    !Me.HasAura("Bear Form") &&
                    DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena) &&
                    !Me.Mounted && !Me.HasAura("Travel Form"),
                    Spell.BuffSelf("Cat Form")
                    ),
                new Decorator(
                    ret =>
                    Me.HasAura("Cat Form") &&
                    (DruidSettings.PvPStealth && (Battlegrounds.IsInsideBattleground 
                    || Me.CurrentMap.IsArena)),
                    Spell.BuffSelf("Prowl")
                    )*/
                );
        }

        #endregion

        #region Combat Buffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, DruidAllSpecs, WoWContext.Normal)]
        public static Composite CreateDruidNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Spell.Buff("Innervate", ret => StyxWoW.Me.ManaPercent <= SingularSettings.Instance.Druid.InnervateMana),
                        Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < 50 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
                        Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
                        Spell.Cast("Hibernate", 
                            ctx => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(
                                u => !u.IsMoving && u.Distance > 10 && Me.CurrentTarget != u
                                    && (u.IsBeast || u.IsDragon)))
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, (WoWSpec)int.MaxValue, WoWContext.Instances)]
        public static Composite CreateDruidInstanceCombatBuffs()
        {
            return new PrioritySelector(
                ctx =>
                Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ??
                Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead),
                new Decorator(
                    ret => ret != null ,
                    new PrioritySelector(
                        Spell.WaitForCast(true),
                        Movement.CreateMoveToLosBehavior(ret => (WoWPlayer) ret),
                        new Decorator(ret => DruidSettings.UseRebirth,
                                      Spell.Cast("Rebirth", ret => (WoWPlayer) ret)),
                        Movement.CreateMoveToTargetBehavior(true, 35f)))
                );
        }

        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateDruidNonRestoHealNormal()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new Sequence(
                        new PrioritySelector(

                            Spell.Heal("Healing Touch",
                                       ret => Me.HealthPercent <= 75
                                           && Me.ActiveAuras.ContainsKey("Predator's Swiftness")),

                            Spell.Buff("Might of Ursoc", ret => Me.HealthPercent < 20),
                            Spell.Heal("Renewal", ret => Me.HealthPercent < DruidSettings.RenewalHealth ),
                            Spell.Cast("Cenarion Ward", ctx => Me, ret => Me.HealthPercent < 75 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),

                            new Decorator(
                                ret => Me.HealthPercent < 60 && Spell.GetSpellCooldown("Nature's Swiftness") == TimeSpan.Zero,
                                new Sequence(
                                    Spell.BuffSelf("Nature's Swiftness"),
                                    Spell.Heal("Healing Touch")
                                    )
                                ),

                            Spell.Heal("Rejuvenation", ret => Me.HealthPercent <= 60 && !Me.HasAura("Rejuvenation")),

                            Spell.Heal("Healing Touch", ret => Me.HealthPercent <= 25)
                            ),

                        new Action( ret => Logger.Write( "DRUID.HEAL"))
                        )
                    )
                );
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateBalanceAndDruidFeralRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(false),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(false,false), 
                    new PrioritySelector(
                        Spell.Heal( "Healing Touch", ctx => Me, ret => Me.GetPredictedHealthPercent(true) <= SingularSettings.Instance.IgnoreHealTargetsAboveHealth ),
                        Rest.CreateDefaultRestBehaviour(),
                        Spell.Resurrect("Revive")
                        )
                    )
                );
        }

        #endregion


#if NOT_IN_USE
        public static Composite CreateEscapeFromCc()
        {
            return
                new PrioritySelector(
                    Spell.Cast("Dash",
                               ret =>
                               DruidSettings.PvPRooted &&
                               Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                               Me.Shapeshift == ShapeshiftForm.Cat),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPRooted &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted) &&
                         Me.Shapeshift == ShapeshiftForm.Cat && SpellManager.HasSpell("Dash") &&
                         SpellManager.Spells["Dash"].Cooldown),
                        new Sequence(
                            new Action(ret => SpellManager.Cast(WoWSpell.FromId(77764))
                                )
                            )),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Cat),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Cat Form\")")
                                )
                            )
                        ),
                    new Decorator(
                        ret =>
                        (DruidSettings.PvPSnared &&
                         Me.HasAuraWithMechanic(WoWSpellMechanic.Snared) &&
                         !Me.ActiveAuras.ContainsKey("Crippling Poison") &&
                         Me.Shapeshift == ShapeshiftForm.Bear),
                        new Sequence(
                            new Action(ret => Lua.DoString("RunMacroText(\"/Cast !Bear Form\")")
                                )
                            )
                        )
                    );
        }

        public static Composite CreateCycloneAdd()
        {
            return
                new PrioritySelector(
                    ctx =>
                    Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForCyclone),
                    new Decorator(
                        ret =>
                        ret != null && DruidSettings.PvPccAdd &&
                        Me.ActiveAuras.ContainsKey("Predator's Swiftness") &&
                        Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Polymorph")),
                        new PrioritySelector(
                            Spell.Buff("Cyclone", ret => (WoWUnit) ret))));
        }

        private static bool IsViableForCyclone(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (Me.CurrentTarget != null && Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (Me.GroupInfo.IsInParty &&
                Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }
#endif

        #region Nested type: Talents

        internal enum Talents
        {
            FelineSwiftness = 1,
            DispacerBeast,
            WildCharge,
            NaturesSwiftness,
            Renewal,
            CenarionWard,
            FaerieSwarm,
            MassEntanglement,
            Typhoon,
            SoulOfTheForest,
            Incarnation,
            ForceOfNature,
            DisorientingRoar,
            UrsolsVortex,
            MightyBash,
            HeartOfTheWild,
            DreamOfCenarius,
            NaturesVigil
        }

        #endregion

   }
}