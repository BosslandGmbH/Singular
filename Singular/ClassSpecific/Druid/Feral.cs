#region

using System;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Collections.Generic;
using CommonBehaviors.Actions;
using Styx.WoWInternals;
using System.Drawing;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        public delegate IEnumerable<WoWUnit> EnumWoWUnitDelegate(object context);

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings DruidSettings { get { return SingularSettings.Instance.Druid(); } }

        #region Common

        [Behavior(BehaviorType.Rest, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralDruidRest()
        {
            return new PrioritySelector(
                new Throttle(10,
                    new Decorator(
                        ret => SpellManager.HasSpell("Savage Roar")
                            && Me.RawComboPoints > 0
                            && Me.ComboPointsTarget != 0
                            && null != ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget)
                            && Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds < (Me.RawComboPoints * 6 + 6),
                        new Sequence(
                            new Action(r => Logger.WriteDebug("cast Savage Roar to use {0} points on corpse of {1} since buff has {2} seconds left", Me.RawComboPoints, ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget).SafeName(), Me.GetAuraTimeLeft("Savage Roar", true).TotalSeconds)),
                            Spell.Cast("Savage Roar", on => ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget))
                            )
                        )
                    ),

                Common.CreateProwlBehavior(ret => Me.HasAura("Drink") || Me.HasAura("Food")),

                new Decorator(
                    ret => !Me.HasAura("Drink") && !Me.HasAura("Food")
                        && Me.HasAura("Predatory Swiftness")
                        && (Me.GetPredictedHealthPercent(true) < 95 || (Common.HasTalent( DruidTalents.DreamOfCenarius) && !Me.HasAuraExpired("Dream of Cenarius"))),
                    new PrioritySelector(
                        new Action(r => { Logger.WriteDebug("Druid Rest Swift Heal @ {0:F1}% and moving:{1} in form:{2}", Me.HealthPercent, Me.IsMoving, Me.Shapeshift); return RunStatus.Failure; }),
                        Spell.Cast("Healing Touch",
                            mov => true,
                            on => Me,
                            req => true,
                            cancel => false)
                        )
                    )

                // remainder of rest behavior in common.cs CreateNonRestoDruidRest()                     
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateFeralPreCombatBuffForSymbiosis( )
        {
            return Common.CreateDruidCastSymbiosis(on => GetFeralBestSymbiosisTargetForBattlegrounds());
        }

        private static WoWPlayer GetFeralBestSymbiosisTargetForBattlegrounds()
        {
            return Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Shaman)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin)
                ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight)));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior( "Pull" ),
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsAboveTheGround(),
                            new PrioritySelector(
                                Spell.Buff("Faerie Fire", ret => Me.CurrentTarget.Distance < 35),
                                Spell.Cast("Moonfire", ret => Me.CurrentTarget.Distance < 40),
                                Movement.CreateMoveToTargetBehavior(true, 27f)
                                )),

                        Spell.BuffSelf("Cat Form"),

                        Common.CreateProwlBehavior(),

                        CreateFeralWildChargeBehavior(),

                        // only Dash if we dont have WC or WC was cast more than 2 seconds ago

                        new Decorator(
                            ret => Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Spell.BuffSelf("Dash", 
                                    ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > 15 
                                        && Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13 ),
                                Spell.Cast("Ravage", ret => Me.IsSafelyBehind(Me.CurrentTarget)),
                                Spell.Cast("Pounce")
                                )
                            ),
                        Spell.Buff("Rake"),
                        Spell.Cast("Mangle")
                        )
                    ),

                // Move to Melee, going behind target if prowling 
                new Decorator(
                    ret => Me.GotTarget && Me.HasAura("Prowl"),
                    Movement.CreateMoveBehindTargetBehavior()
                    ),
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Throttle CreateFeralWildChargeBehavior()
        {
            // save WC for later if Dash is active. also throttle to deal with possible pathing issues
            return new Throttle(7,
                new Sequence(
                    Spell.Cast("Wild Charge", ret => MovementManager.IsClassMovementAllowed && !Me.HasAura("Dash")),
                    new Wait(1, until => !Me.GotTarget || Me.CurrentTarget.IsWithinMeleeRange, new ActionAlwaysSucceed())
                    )
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new Decorator(
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(), 
                new PrioritySelector(

                    // cast cat form if not in combat and moving, but only if not recent shapeshift error
                    new Throttle( 10, Spell.BuffSelf( "Cat Form", ret => Me.IsMoving && !RecentShapeshiftErrorOccurred )),

                    // cancel form if we get a shapeshift error 
                    new Throttle( 5,
                        new Decorator(
                            ret => !Me.IsMoving && Me.Shapeshift != ShapeshiftForm.Normal && SingularRoutine.IsQuesting && RecentShapeshiftErrorOccurred,
                            new Action(ret =>
                            {
                                string formName = Me.Shapeshift.ToString() + " Form";
                                Logger.Write("/cancel [{0}] due to shapeshift error and prevent out of combat {1:F0} seconds while Questing", formName, (SuppressShapeshiftUntil - DateTime.Now).TotalSeconds );
                                Me.CancelAura(formName);
                            })
                            )
                        )
                    )
                );
        }

        private static DateTime SuppressShapeshiftUntil
        {
            get
            {
                return Utilities.EventHandlers.LastShapeshiftError.AddSeconds(60);
            }
        }

        private static bool RecentShapeshiftErrorOccurred
        {
            get
            {
                return SuppressShapeshiftUntil > DateTime.Now;
            }
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralNormalCombatBuffs()
        {
            return new Decorator( 
                ret => !Spell.IsCastingOrChannelling() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(
                    Spell.BuffSelf("Cat Form")
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior("Combat"),
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action( ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateFeralAoeCombat(),

#region Symbiosis
                        new Decorator(
                            ret => Me.HasAura( "Symbiosis") && !Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Spell.BuffSelf("Feral Spirit", 
                                    ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances 
                                        || Me.CurrentTarget.IsBoss()
                                        || Me.CurrentTarget.IsPlayer
                                        || Unit.NearbyUnfriendlyUnits.Count( u => u.IsTargetingMeOrPet ) >= 2),

                                Spell.Cast("Shattering Blow", ret => Me.CurrentTarget.IsPlayer && Me.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield")),

                                Spell.Cast("Death Coil", ret => !Me.CurrentTarget.IsWithinMeleeRange )
                                )
                            ),
#endregion

                        //Single target
                        Spell.Cast("Faerie Fire", ret =>!Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        new Throttle( Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery")))),

                        new Throttle( Spell.BuffSelf("Tiger's Fury", 
                                   ret => Me.EnergyPercent <= 35 
                                       && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                       && !Me.HasAura("Berserk")
                                       )),

                        new Throttle( 
                            Spell.BuffSelf("Berserk", 
                                ret => Me.HasAura("Tiger's Fury") 
                                    && (Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (SingularRoutine.CurrentWoWContext != WoWContext.Instances && Me.CurrentTarget.TimeToDeath() >= 20 ))
                                )
                            ),

                        new Throttle( Spell.Cast("Nature's Vigil", ret => Me.HasAura("Berserk"))),
                        Spell.Cast("Incarnation", ret => Me.HasAura("Berserk")),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite", 
                            ret => Me.ComboPoints >= 5
                                && (Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 6 || Me.CurrentTarget.TimeToDeath() < 6)),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.TimeToDeath() >= 7
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 1),

                        Spell.Cast("Ravage"),

                        Spell.Buff("Rake", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3),

                        Spell.Cast("Shred", 
                            ret =>  (Me.CurrentTarget.MeIsSafelyBehind || (TalentManager.HasGlyph("Shred") && (Me.HasAnyAura("Tiger's Fury", "Berserk"))))),

                        Spell.Cast("Mangle"),

                        Spell.CastOnGround("Force of Nature", 
                            u => (Me.CurrentTarget ?? Me) .Location,
                            ret => StyxWoW.Me.CurrentTarget != null 
                                && StyxWoW.Me.CurrentTarget.Distance < 40
                                && SpellManager.HasSpell("Force of Nature")),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > (Me.CurrentTarget.IsPlayer ? 10 : 15),
                            new PrioritySelector(
                                CreateFeralWildChargeBehavior(),
                                Spell.BuffSelf("Dash", ret => Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13 )
                                )
                            )

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static IEnumerable<WoWUnit> _aoeColl;

        private static Composite CreateFeralAoeCombat()
        {
            return new PrioritySelector(

                new Action( ret => {
                    _aoeColl = Unit.NearbyUnfriendlyUnits.Where(u => u.MeleeDistance() < 8f);
                    return RunStatus.Failure;
                    }),

                new Decorator(ret => Spell.UseAOE && _aoeColl.Count() >= 3 && !_aoeColl.Any(m => m.IsCrowdControlled()),

                    new PrioritySelector(

                        Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),

                        Spell.Cast("Thrash", ret => _aoeColl.Any(m => !m.HasMyAura("Thrash"))),

                        Spell.BuffSelf("Tiger's Fury",
                            ret => Me.EnergyPercent <= 35
                                && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                && !Me.HasAura("Berserk")),

                        Spell.BuffSelf("Berserk", ret => Me.HasAura("Tiger's Fury") && SingularRoutine.CurrentWoWContext != WoWContext.Instances),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite",
                            ret => Me.ComboPoints >= 5
                                && (Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 6 || Me.CurrentTarget.TimeToDeath() < 8)),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.TimeToDeath() >= 8
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 1),

                        Spell.Cast("Swipe", ret => Me.CurrentTarget.IsWithinMeleeRange),

                        Movement.CreateMoveToMeleeBehavior(true)
                        )
                    ),

                // otherwise, try and keep Rake up on mobs allowing some AoE dmg without breaking CC
                Spell.Cast( "Rake", 
                    ret => _aoeColl.FirstOrDefault( 
                        m => m.Guid != Me.CurrentTargetGuid 
                            && m.IsWithinMeleeRange 
                            && !m.HasMyAura("Rake") 
                            && Me.IsSafelyFacing(m) 
                            && !m.IsCrowdControlled()))
                );
        }



        #region Diagnostics

        private static Composite CreateFeralDiagnosticOutputBehavior(string context = null)
        {
            if (context == null)
                context = "...";
            else
                context = "<<" + context + ">>";

            return new Decorator(
                ret => SingularSettings.Debug,
                new ThrottlePasses(1,
                    new Action(ret =>
                    {
                        string log;
                        log = string.Format(context + " h={0:F1}%/e={1:F1}%/m={2:F1}%, shape={3}, prowl={4}, savage={5}, tfury={6}, bersrk={7}, predswft={8}, combo={9}",
                            Me.HealthPercent,
                            Me.EnergyPercent,
                            Me.ManaPercent,
                            Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                            Me.HasAura("Prowl").ToYN(),
                            (long)Me.GetAuraTimeLeft("Savage Roar", true).TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Tiger's Fury", true).TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds,
                            Me.ComboPoints 
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            log += string.Format(", th={0:F1}%, dist={1:F1}, face={2}, loss={3}, dead={4} secs, rake={5}, rip={6}",
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target),
                                target.InLineOfSpellSight,
                                target.TimeToDeath(),
                                (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Rip", true).TotalMilliseconds
                                );
                        }

                        Logger.WriteDebug(Color.AntiqueWhite, log);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

        #endregion
    }
}