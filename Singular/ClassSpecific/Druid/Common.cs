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
using Styx.CommonBot;
using Singular.Managers;
using CommonBehaviors.Actions;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Common
    {
        public static ShapeshiftForm WantedDruidForm { get; set; }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static bool HasTalent(DruidTalents tal) { return TalentManager.IsSelected((int)tal); }

        public const WoWSpec DruidAllSpecs = (WoWSpec)int.MaxValue;

        #region PreCombat Buffs

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid)]
        public static Composite CreateDruidPreCombatBuff()
        {
            // Cast motw if player doesn't have it or if in instance/bg, out of combat and 'Buff raid with Motw' is true or if in instance and in combat and both CatRaidRebuff and 'Buff raid with Motw' are true
            return new PrioritySelector(

                PartyBuff.BuffGroup( "Mark of the Wild", ret => !Me.HasAura("Prowl") && !Me.Combat )

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
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(
                    Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                    Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < 50 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
                    Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),

                    // will hibernate only if can cast in form, or already left form for some other reason
                    Spell.Buff("Hibernate", 
                        ctx => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(
                            u => (u.IsBeast || u.IsDragon)
                                && (Me.HasAura("Predatory Swiftness") || (!u.IsMoving  && Me.Shapeshift == ShapeshiftForm.Normal))
                                && (!Me.GotTarget || Me.CurrentTarget.Location.Distance(u.Location) > 10 )
                                && Me.CurrentTargetGuid != u.Guid
                                && !u.HasMyAura("Hibernate") 
                                )
                            )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, (WoWSpec)int.MaxValue, WoWContext.Instances | WoWContext.Battlegrounds)]
        public static Composite CreateDruidInstanceCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    CreateRebirthBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead)),

                    Spell.BuffSelf("Celestial Alignment", ret => Me.CurrentTarget != null && Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),

                    // to do:  time ICoE at start of eclipse
                    Spell.BuffSelf("Incarnation: Chosen of Elune"),

                    Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location, ret => true),

                    Spell.Buff("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                    Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < 50 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
                    Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),

                    Spell.BuffSelf("Nature's Vigil")
                    )
                );
        }

/*
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidBalance, WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral , WoWContext.Instances, 1)]
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian , WoWContext.Instances, 1)]
        public static Composite CreateNonRestoDruidInstanceCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(

                    new Decorator(
                        ret => HasTalent(DruidTalents.DreamOfCenarius) && !Me.HasAura("Dream of Cenarius"),
                        new PrioritySelector(
                            Spell.Heal("Healing Touch", ret => Me.ActiveAuras.ContainsKey("Predatory Swiftness")),
                            CreateNaturesSwiftnessHeal(on => GetBestHealTarget())
                            )
                        )
                    )
                );
        }
*/
        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds, 2)]
        public static Composite CreateFeralDruidBattlegroundCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(
                    Spell.Buff("Cyclone",
                        ctx => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(
                            u => Me.HasAura("Predatory Swiftness")
                                && u.IsCasting 
                                && Me.GotTarget
                                && Me.CurrentTargetGuid != u.Guid
                                )
                            )
                    )
                );
        }

        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateDruidNonRestoHeal()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),

                    new Sequence(
                        new PrioritySelector(

                            Spell.Cast("Healing Touch",
                                ret => Me.HealthPercent <= 80
                                    && Me.ActiveAuras.ContainsKey("Predatory Swiftness")),

                            Spell.Cast("Renewal", ret => Me.HealthPercent < DruidSettings.RenewalHealth ),
                            Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < 75 || Unit.NearbyUnitsInCombatWithMe.Count() >= 2),

                            CreateNaturesSwiftnessHeal( ret => Me.HealthPercent < 60),

                            new Decorator(
                                ret => Me.HealthPercent < 40,
                                new PrioritySelector(

                                    Spell.Cast("Disorienting Roar", ret => Me.HealthPercent <= 25 && Unit.NearbyUnfriendlyUnits.Any(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet))),
                                    Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < 25),

                                    // heal out of form at this point (try to Barkskin at least)
                                    new Throttle( Spell.BuffSelf( "Barkskin")),

                                    new Decorator(
                                        ret => SingularRoutine.CurrentWoWContext != WoWContext.Battlegrounds,
                                        new PrioritySelector(
                                            Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation",1)),
                                            Spell.Cast("Healing Touch", on => Me)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static Composite CreateNaturesSwiftnessHeal(SimpleBooleanDelegate requirements = null)
        {
            return CreateNaturesSwiftnessHeal(on => Me, requirements);
        }

        public static Composite CreateNaturesSwiftnessHeal(UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements = null)
        {
            return new Decorator(
                ret => onUnit != null && onUnit(ret) != null && requirements != null && requirements(ret),
                new Sequence(
                    Spell.BuffSelf("Nature's Swiftness"),
                    new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Nature's Swiftness"), new ActionAlwaysSucceed()),
                    Spell.Cast("Healing Touch", ret => false, onUnit, req => true)
                    )
                );
        }

        public static WoWUnit GetBestHealTarget()
        {
            if (SingularRoutine.CurrentWoWContext == WoWContext.Normal || Me.HealthPercent < 40)
                return Me;

            return Unit.NearbyFriendlyPlayers.Where(p=>p.IsAlive).OrderBy(k=>k.GetPredictedHealthPercent(false)).FirstOrDefault();
        }

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidBalance)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateNonRestoDruidRest()
        {
            return new PrioritySelector(
                Spell.WaitForCast(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(
                        new Decorator(
                            ret => !Me.HasAura("Drink") && !Me.HasAura("Food")
                                && (Me.GetPredictedHealthPercent(true) < SingularSettings.Instance.MinHealth || (Me.Shapeshift == ShapeshiftForm.Normal && Me.GetPredictedHealthPercent(true) < 85))
                                && SpellManager.HasSpell("Healing Touch") && SpellManager.CanCast("Healing Touch", Me, false, false),
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(),
                                new Action(r => { Logger.WriteDebug("Druid Rest Heal @ {0:F1}% and moving:{1} in form:{2}", Me.HealthPercent, Me.IsMoving, Me.Shapeshift ); return RunStatus.Failure; }),
                                Spell.Cast("Healing Touch",
                                    mov => true,
                                    on => Me,
                                    req => true,
                                    cancel => Me.HealthPercent > 92)
                                )
                            ),

                        Rest.CreateDefaultRestBehaviour(null, "Revive")
                        )
                    )
                );
        }

        #endregion

        internal static Composite CreateProwlBehavior(SimpleBooleanDelegate req = null)
        {
            return new Sequence(
                Spell.BuffSelf("Prowl", ret => Me.Shapeshift == ShapeshiftForm.Cat && (req == null || req(ret))),
                new Wait(TimeSpan.FromMilliseconds(500), ret => Me.HasAura("Prowl"), new ActionAlwaysSucceed())
                );
        }

        public static Composite CreateRebirthBehavior(UnitSelectionDelegate onUnit)
        {
            if ( !DruidSettings.UseRebirth )
                return new PrioritySelector();

            if ( onUnit == null)
            {
                Logger.WriteDebug( "CreateRebirthBehavior: error - onUnit == null");
                return new PrioritySelector();
            }

            return new PrioritySelector(
                ctx => onUnit(ctx),
                new Decorator(
                    ret => onUnit(ret) != null && Spell.GetSpellCooldown("Rebirth") == TimeSpan.Zero,
                    new PrioritySelector(
                        Spell.WaitForCast(true),
                        Movement.CreateMoveToRangeAndStopBehavior( ret => (WoWUnit) ret, range => 40f),
                        new Decorator(
                            ret => !Spell.IsGlobalCooldown(),
                            Spell.Cast("Rebirth", ret => (WoWUnit) ret)
                            )
                        )
                    )
                );
        }

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
                        Me.ActiveAuras.ContainsKey("Predatory Swiftness") &&
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

   }
    #region Nested type: Talents

    public enum DruidTalents
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