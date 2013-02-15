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

        [Behavior(BehaviorType.LossOfControl, WoWClass.Druid)]
        public static Composite CreateDruidLossOfControlBehavior()
        {
            return new Decorator(
                ret => !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                new PrioritySelector(
                    Spell.BuffSelf("Barkskin")
                    )
                );
        }
        

        #region Combat Buffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, DruidAllSpecs, WoWContext.Normal)]
        public static Composite CreateDruidCombatBuffsNormal()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
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
                        ),

                // combat buffs - make sure we have target and in range and other checks
                // ... to avoid wastine cooldowns
                new Decorator(
                    ret => Me.GotTarget
                        && (Me.CurrentTarget.IsPlayer || Unit.NearbyUnitsInCombatWithMe.Count() >= 3)
                        && Me.SpellDistance(Me.CurrentTarget) < ((Me.Specialization == WoWSpec.DruidFeral || Me.Specialization == WoWSpec.DruidGuardian) ? 8 : 40)
                        && Me.CurrentTarget.InLineOfSight
                        && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),
                        Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location, ret => true),
                // to do:  time ICoE at start of eclipse
                        Spell.BuffSelf("Incarnation: Chosen of Elune"),
                        Spell.BuffSelf("Nature's Vigil")
                        )
                    )
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, (WoWSpec)int.MaxValue, WoWContext.Instances | WoWContext.Battlegrounds)]
        public static Composite CreateDruidCombatBuffsInstance()
        {
            return new PrioritySelector(

                CreateRebirthBehavior(ctx => Group.Tanks.FirstOrDefault(t => !t.IsMe && t.IsDead) ?? Group.Healers.FirstOrDefault(h => !h.IsMe && h.IsDead)),

                Spell.Buff("Innervate", ret => StyxWoW.Me.ManaPercent <= DruidSettings.InnervateMana),
                Spell.Cast("Barkskin", ctx => Me, ret => Me.HealthPercent < DruidSettings.Barkskin || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),
                Spell.Cast("Disorenting Roar", ctx => Me, ret => Me.HealthPercent < 40 || Unit.NearbyUnitsInCombatWithMe.Count() >= 3),

                // combat buffs - make sure we have target and in range and other checks
                // ... to avoid wastine cooldowns
                new Decorator(
                    ret => Me.GotTarget 
                        && (Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsBoss())
                        && Me.SpellDistance( Me.CurrentTarget) < ((Me.Specialization == WoWSpec.DruidFeral || Me.Specialization == WoWSpec.DruidGuardian) ? 8 : 40)
                        && Me.CurrentTarget.InLineOfSight 
                        && Me.IsSafelyFacing(Me.CurrentTarget),
                    new PrioritySelector(
                        Spell.BuffSelf("Celestial Alignment", ret => Spell.GetSpellCooldown("Celestial Alignment") == TimeSpan.Zero && PartyBuff.WeHaveBloodlust),
                        Spell.CastOnGround("Force of Nature", ret => StyxWoW.Me.CurrentTarget.Location, ret => true),
                        // to do:  time ICoE at start of eclipse
                        Spell.BuffSelf("Incarnation: Chosen of Elune"),
                        Spell.BuffSelf("Nature's Vigil")
                        )
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
            return new PrioritySelector(
                Spell.Buff("Cyclone",
                    ctx => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(
                        u => Me.HasAura("Predatory Swiftness")
                            && u.IsCasting 
                            && Me.GotTarget
                            && Me.CurrentTargetGuid != u.Guid
                        )
                    )
                );
        }

        #endregion

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral)]
        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CreateDruidNonRestoHeal()
        {
            return new PrioritySelector(

                // keep rejuv up 
                Spell.Cast("Rejuvenation", on => Me, 
                    ret => Me.HasAuraExpired("Rejuvenation", 1) // && Me.HealthPercent < 95
                        && (Me.Shapeshift == ShapeshiftForm.Normal || (Me.Specialization == WoWSpec.DruidGuardian && Me.ActiveAuras.ContainsKey("Heart of the Wild")))),

                Spell.Cast("Healing Touch", on => Me, ret => Me.HealthPercent <= 80 && Me.ActiveAuras.ContainsKey("Predatory Swiftness")),
                Spell.Cast("Healing Touch", on => Me, ret => Me.HealthPercent <= 95 && Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalSeconds.Between(1, 3)),

                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < DruidSettings.RenewalHealth ),
                Spell.BuffSelf("Cenarion Ward", ret => Me.HealthPercent < 85 || Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet)) > 1),

                CreateNaturesSwiftnessHeal( ret => Me.HealthPercent < 60),

                Spell.Cast("Disorienting Roar", ret => Me.HealthPercent <= 25 && Unit.NearbyUnfriendlyUnits.Any(u => u.Aggro || (u.Combat && u.IsTargetingMeOrPet))),
                Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < 25),

                // heal out of form at this point (try to Barkskin at least)
                new Throttle( Spell.BuffSelf( "Barkskin", ret => Me.HealthPercent < DruidSettings.Barkskin)),

                // for a lowbie Feral or a Bear not serving as Tank in a group
                new Decorator(
                    ret => Me.HealthPercent < 40 && !SpellManager.HasSpell("Predatory Swiftness") && !Group.MeIsTank,
                    new PrioritySelector(
                        Spell.Cast("Rejuvenation", on => Me, ret => Me.HasAuraExpired("Rejuvenation",1)),
                        Spell.Cast("Healing Touch", on => Me)
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

        private static WoWUnit _targetSymb;

        public static Composite CreateDruidCastSymbiosis(UnitSelectionDelegate onUnit)
        {
            return new Decorator(
                ret => DruidSettings.UseSymbiosis
                    && !Me.IsMoving
                    && (SingularRoutine.CurrentWoWContext == WoWContext.Instances || (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds && ( Battlegrounds.BattlefieldStartTime - DateTime.Now).TotalSeconds < 5))
                    && SpellManager.HasSpell("Symbiosis")
                    && !Me.HasAura("Symbiosis"),
                new Sequence(
                    new Action(r => _targetSymb = onUnit(r)),
                    new Decorator(
                        ret => _targetSymb != null,
                        new Sequence(
                            new Action(r =>
                            {
                                if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                                {
                                    Logger.WriteDebug("Seconds remain to start={0} secs", (Battlegrounds.BattlefieldStartTime - DateTime.Now).TotalSeconds);
                                }
                            }),
                            new Action(r => _targetSymb.Target()),
                            new Wait(1, until => Me.CurrentTargetGuid == _targetSymb.Guid, new ActionAlwaysSucceed()),
                            Spell.Buff("Symbiosis", false, on => _targetSymb, ret => _targetSymb.Distance < 30),
                            new Action(r => Blacklist.Add(_targetSymb.Guid, BlacklistFlags.Combat, TimeSpan.FromSeconds(30))),
                            new Action(r => Me.ClearTarget()),
                            // new Wait(1, until => Me.CurrentTargetGuid != _targetSymb.Guid, new ActionAlwaysSucceed()),
                            new Wait(TimeSpan.FromMilliseconds(500), until => Me.HasAura("Symbiosis"), new ActionAlwaysSucceed()),
                            new Action(r => {
                                SpellFindResults sfr;
                                if (!SpellManager.FindSpell("Symbiosis", out sfr))
                                    Logger.WriteDebug("Symbiosis: unable to find a spell gained ?");
                                else if (sfr.Override == null)
                                    Logger.Write("error: HonorBuddy has not overridden Symbiosis with spell we gained");
                                else
                                {
                                    Logger.Write("Symbiosis: we gained {0} #{1}", sfr.Override.Name, sfr.Override.Id);
                                    Logger.WriteDebug("Symbiosis: CanCast {0} is {1}", sfr.Override.Name, SpellManager.CanCast(sfr.Override.Name, true));
                                }
                                })
                            )
                        )
                    )
                );
        }

        public static bool IsValidSymbiosisTarget(WoWPlayer p)
        {
            if (p.IsHorde != Me.IsHorde)
                return false;

            if (Blacklist.Contains(p.Guid, BlacklistFlags.Combat))
                return false;

            if (!p.IsAlive)
                return false;

            if (p.Level < 87)
                return false;

            if (p.Combat)
                return false;

            if (p.Distance > 28)
                return false;

            if (p.HasAura("Symbiosis"))
                return false;

            if (!p.InLineOfSpellSight)
                return false;

            return true;
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