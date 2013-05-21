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
using Styx.CommonBot.POI;
using Styx.Helpers;

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
                            Spell.Cast(52610, on => ObjectManager.GetObjectByGuid<WoWUnit>(Me.ComboPointsTarget))
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
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsAboveTheGround(),
                            new PrioritySelector(
                                Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => Me.CurrentTarget.Distance < 35),
                                Spell.Cast("Moonfire", ret => Me.CurrentTarget.Distance < 40),
                                Movement.CreateMoveToUnitBehavior( on => StyxWoW.Me.CurrentTarget, 27f, 22f)
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
                    Spell.CastHack("Wild Charge", ret => MovementManager.IsClassMovementAllowed && !Me.HasAura("Dash")),
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

                    // cast cat form 
                    // since this check comes while not in combat (so will be doing other things like Questing) need to add some checks:
                    // - only if Moving
                    // - only if No Recent Shapefhift Error (since form may have resulted from error in picking up Quest, completing Quest objectives, or turning in Quest)
                    new Throttle(10, Spell.BuffSelf("Cat Form", ret => Me.IsMoving && !Common.RecentShapeshiftErrorOccurred && !Me.IsSwimming && !Me.HasAnyAura("Travel Form", "Aquatic Form"))),

                    // cancel form if we get a shapeshift error 
                    new Throttle( 5,
                        new Decorator(
                            ret => !Me.IsMoving && Me.Shapeshift != ShapeshiftForm.Normal && SingularRoutine.IsQuestBotActive && Common.RecentShapeshiftErrorOccurred,
                            new Action(ret =>
                            {
                                string formName = Me.Shapeshift.ToString() + " Form";
                                Logger.Write("/cancel [{0}] due to shapeshift error in Questing; disabling form for {1:F0} secs while not in combat", formName, (Common.SuppressShapeshiftUntil - DateTime.Now).TotalSeconds );
                                Me.CancelAura(formName);
                            })
                            )
                        ),

                    Common.CreateProwlBehavior(
                        ret => DruidSettings.ProwlAlways
                            && BotPoi.Current.Type != PoiType.Loot
                            && BotPoi.Current.Type != PoiType.Skin
                            && !ObjectManager.GetObjectsOfType<WoWUnit>().Any(u => u.IsDead && ((CharacterSettings.Instance.LootMobs && u.CanLoot && u.Lootable) || (CharacterSettings.Instance.SkinMobs && u.Skinnable && u.CanSkin)) && u.Distance < CharacterSettings.Instance.LootRadius)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Heal, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 999)]
        public static Composite CreateFeralCombatHeal()
        {
            return new PrioritySelector(
                CreateFeralDiagnosticOutputBehavior("Combat")
                );
        }


        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All, 1)]
        public static Composite CreateFeralCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form")
                );
        }

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

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Normal | WoWContext.Battlegrounds)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
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
                                Common.SymbCast(Symbiosis.FeralSpirit, on => Me.CurrentTarget, ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.CurrentTarget.IsBoss() || Unit.NearbyUnfriendlyUnits.Count( u => u.IsTargetingMeOrPet ) >= 2),
                                Common.SymbCast(Symbiosis.ShatteringBlow, on => Me.CurrentTarget, ret => Me.CurrentTarget.IsPlayer && Me.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield")),
                                Common.SymbCast(Symbiosis.DeathCoil, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange),
                                Common.SymbCast(Symbiosis.Clash, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),
#endregion

                        //Single target
                        Common.CreateFaerieFireBehavior( on => Me.CurrentTarget, req => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        ///
                        /// Savage Roar - original spell id = 52610, override is 127538.  both spells valid but there is not an obvious need for the
                        /// override.  Additionally, CanCast AWLAYS fails for 127538 meaning CanCast("Spell Manager") always fails.  workaround
                        /// is to cast by id
                        ///
                        // new Throttle(Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery")))),
                        new Throttle(Spell.Cast( 52610, ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery")))),

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

                        // bite if rip on for awhile or target dying soon
                        Spell.Cast("Ferocious Bite", 
                            ret => (Me.ComboPoints >= 5 && !Me.HasAuraExpired("Rip", 6))
                                || Me.ComboPoints >= Me.CurrentTarget.TimeToDeath(99)),

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


        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.Instances )]
        public static Composite CreateFeralCombatInstances()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action(ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; }),

                        Helpers.Common.CreateInterruptBehavior(),

                        CreateFeralAoeCombat(),

            #region Symbiosis
                        new Decorator(
                            ret => Me.HasAura("Symbiosis") && !Me.HasAura("Prowl"),
                            new PrioritySelector(
                                Common.SymbCast(Symbiosis.FeralSpirit, on => Me.CurrentTarget, ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances || Me.CurrentTarget.IsBoss() || Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                                Common.SymbCast(Symbiosis.ShatteringBlow, on => Me.CurrentTarget, ret => Me.CurrentTarget.IsPlayer && Me.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield")),
                                Common.SymbCast(Symbiosis.DeathCoil, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange),
                                Common.SymbCast(Symbiosis.Clash, on => Me.CurrentTarget, ret => !Me.CurrentTarget.IsWithinMeleeRange)
                                )
                            ),
            #endregion

                        // 1. Keep Faerie Fire up (if no other armor debuff).
                        Common.CreateFaerieFireBehavior(on => Me.CurrentTarget, req => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        new Decorator(
                            ret => Me.GotTarget
                                && Me.SpellDistance(Me.CurrentTarget) < 8,

                            new PrioritySelector(
                                // 2. Keep Savage Roar up
                                // Note:  Savage Roar bugged due to override?  cast by id to work around CanCast always failing on override
                                // Spell.Cast( "Savage Roar", req => Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery")),
                                Spell.Cast(52610, ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),

                                // 3. Use Tiger’s Fury/Nature's Vigil/Incarnation/Berserking/Force of Nature*
                                new Throttle(Spell.BuffSelf("Tiger's Fury",
                                    ret => Me.EnergyPercent <= 35
                                        && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                        && !Me.HasAura("Berserk")
                                        )),

                                new Throttle(
                                    Spell.BuffSelf("Berserk",
                                        ret => Me.HasAura("Tiger's Fury")
                                            && (Me.CurrentTarget.IsBoss() || Me.CurrentTarget.IsPlayer || (SingularRoutine.CurrentWoWContext != WoWContext.Instances && Me.CurrentTarget.TimeToDeath() >= 20))
                                        )
                                    ),

                                new Throttle(Spell.Cast("Nature's Vigil", ret => Me.HasAura("Berserk"))),
                                Spell.Cast("Incarnation", ret => Me.HasAura("Berserk")),

                                Spell.CastOnGround("Force of Nature",
                                    u => (Me.CurrentTarget ?? Me).Location,
                                    ret => StyxWoW.Me.CurrentTarget != null
                                        && StyxWoW.Me.CurrentTarget.Distance < 40
                                        && SpellManager.HasSpell("Force of Nature")),

                                // 4. Use Nature’s Swiftness/Healing touch to generate Wrath of Cenarius procs when GCD will not cause energy cap*
                                // 5. Use Predatory Swiftness to generate Dream of Cenarius procs when GCD will not cause energy cap, preferably at 4CP.**
                                new Decorator(
                                    ret => Me.EnergyPercent < 80 
                                        && Common.HasTalent( DruidTalents.DreamOfCenarius) 
                                        && !Me.HasAura("Wrath of Cenarius"),
                                    new Sequence(
                                        new PrioritySelector(
                                            new Decorator(  ret => Me.HasAura("Predatory Swiftness"), new ActionAlwaysSucceed()),
                                            Spell.BuffSelf( "Nature's Swiftness")
                                            ),
                                        Spell.Cast( "Healing Touch", on => Me )
                                        )
                                    ),

                                new Decorator(
                                    ret => DruidSettings.FeralSpellPriority != 1,
                                    new PrioritySelector(
                                        // made a higher priority to prioritize consuming Omen of Clarity with Thrash if needed
                                        // note:  id used to fix Thrash Spell Override bug (similar to Savage Roar)
                                        Spell.Buff("Thrash", true, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity"), 3),
                                        // Spell.Buff(106832, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity") && Me.CurrentTarget.HasAuraExpired("Thrash", 3)),

                                        // 6. Ferocious Bite if the boss has less than 25% hp remaining and Rip is near expiring.
                                        Spell.Cast("Ferocious Bite", req => Me.CurrentTarget.HealthPercent < 25 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalMilliseconds.Between(150, 6000)),

                                        // 7. Ferocious Bite if you have 5 CP and at least 6 - 10 seconds on Savage Roar and Rip
                                        Spell.Cast("Ferocious Bite", 
                                            req => Me.ComboPoints >= 5
                                                && !Me.HasAuraExpired("Savage Roar", 6)
                                                && !Me.HasAuraExpired("Rip", 6)
                                                ),

                                        // 8. Keep 5 combo point Rip up.
                                        Spell.Buff("Rip", true, on => Me.CurrentTarget, req => Me.ComboPoints >= 5, 3),

                                        // 9. Keep Rake up
                                        Spell.Buff("Rake", true, on => Me.CurrentTarget, req => true, 3),

                                        // 10. Spend Omen of Clarity procs on Thrash if Thrash has less than 6 seconds remaining.
                                        // Spell.Buff("Thrash", true, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity"), 3),
                                        Spell.Buff(106832, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity") && Me.CurrentTarget.HasAuraExpired("Thrash", 3)),

                                        // 11. Ravage to generate combo points if Ravage is available (Incarnation)
                                        Spell.Cast("Ravage", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || Me.HasAnyAura("Incarnation", "Stampede"))),

                                        // 12. Shred to generate combo points if Shred is available (Behind boss, berserk w/glyph, etc)
                                        Spell.Cast("Shred", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || (TalentManager.HasGlyph("Shred") && Me.HasAnyAura("Tiger's Fury", "Berserk")))),

                                        // 13. Use Mangle to generate combo points.
                                        Spell.Cast("Mangle", req => Me.ComboPoints < 5 ),

                                        // 14. Maintain Thrash bleed if it will not interfere with Rake, Rip, or SR uptimes.
                                        Spell.Buff(106832, 
                                            on => Me.CurrentTarget, 
                                            req => Me.CurrentTarget.HasAuraExpired("Thrash", 3)
                                                && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds >= 6
                                                && Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds >= 6
                                                && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 6)
                                        )
                                    ),

                                new Decorator(
                                    ret => DruidSettings.FeralSpellPriority == 1,
                                    new PrioritySelector(

                                        // note:  id used to fix Thrash Spell Override bug (similar to Savage Roar)
                                        // Spell.Buff("Thrash", true, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity"), 3),
                                        Spell.Buff(106832, on => Me.CurrentTarget, req => Me.HasAura("Omen of Clarity") && Me.CurrentTarget.HasAuraExpired("Thrash",3)),

                                        new Decorator(
                                            req => Me.ComboPoints >= 5,
                                            new PrioritySelector(
                                                Spell.Cast("Ferocious Bite", req => Me.CurrentTarget.HealthPercent < 25 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalMilliseconds.Between(150, 6000)),
                                                Spell.Cast("Ferocious Bite",
                                                    req => !Me.HasAuraExpired("Savage Roar", 6)
                                                        && !Me.HasAuraExpired("Rip", 6)
                                                        ),
                                                Spell.Buff("Rip", true, on => Me.CurrentTarget, req => true, 3)
                                                )
                                            ),

                                        new Decorator(
                                            req => Me.ComboPoints < 5,
                                            new PrioritySelector(
                                                Spell.Buff("Rake", true, on => Me.CurrentTarget, req => true, 3),
                                                Spell.Cast("Ravage", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || Me.HasAnyAura("Incarnation", "Stampede"))),
                                                Spell.Cast("Shred", req => Me.ComboPoints < 5 && (Me.IsSafelyBehind(Me.CurrentTarget) || (TalentManager.HasGlyph("Shred") && Me.HasAnyAura("Tiger's Fury", "Berserk")))),
                                                Spell.Cast("Mangle", req => Me.ComboPoints < 5 )
                                                )
                                            )
                                        )
                                    )
                                )
                            ),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed && Me.IsMoving && Me.CurrentTarget.Distance > (Me.CurrentTarget.IsPlayer ? 10 : 15),
                            new PrioritySelector(
                                CreateFeralWildChargeBehavior(),
                                Spell.BuffSelf("Dash", ret => Spell.GetSpellCooldown("Wild Charge", 0).TotalSeconds < 13)
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
            // disable AOE for PVP
            if (SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                return new ActionAlwaysFail();

            return new PrioritySelector(

                new Action( ret => {
                    _aoeColl = Unit.NearbyUnfriendlyUnits.Where(u => u.SpellDistance() < 8f);
                    return RunStatus.Failure;
                    }),

                new Decorator(ret => Spell.UseAOE && _aoeColl.Count() >= 3 && !_aoeColl.Any(m => m.IsCrowdControlled()) && Me.Level >= 22,

                    new PrioritySelector(

                        // hanlde Savage Roar override bug
                        // Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),
                        Spell.Cast(52610, ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery"))),

                        Spell.Cast("Thrash", ret => _aoeColl.Any(m => !m.HasMyAura("Thrash"))),

                        Spell.BuffSelf("Tiger's Fury",
                            ret => Me.EnergyPercent <= 35
                                && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                && !Me.HasAura("Berserk")),

                        Spell.BuffSelf("Berserk", ret => Me.HasAura("Tiger's Fury") && SingularRoutine.CurrentWoWContext != WoWContext.Instances),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite",
                            ret => (Me.ComboPoints >= 5 && !Me.HasAuraExpired("Rip", 6))
                                || Me.ComboPoints >= Me.CurrentTarget.TimeToDeath(99)),

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

            if (!SingularSettings.Debug)
                return new ActionAlwaysFail();

            return new ThrottlePasses(1,
                new Action(ret =>
                {
                    string log;
                    log = string.Format(context + " h={0:F1}%/e={1:F1}%/m={2:F1}%, shape={3}, prowl={4}, savage={5}, tfury={6}, brsrk={7}, predswf={8}, omen={9}, pts={10}",
                        Me.HealthPercent,
                        Me.EnergyPercent,
                        Me.ManaPercent,
                        Me.Shapeshift.ToString().Length < 4 ? Me.Shapeshift.ToString() : Me.Shapeshift.ToString().Substring(0, 4),
                        Me.HasAura("Prowl").ToYN(),
                        (long)Me.GetAuraTimeLeft("Savage Roar", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Tiger's Fury", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Berserk", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Predatory Swiftness", true).TotalMilliseconds,
                        (long)Me.GetAuraTimeLeft("Omen of Clarity", true).TotalMilliseconds,
                        Me.ComboPoints 
                        );

                    WoWUnit target = Me.CurrentTarget;
                    if (target != null)
                    {
                        log += string.Format(", th={0:F1}%, dist={1:F1}, inmelee={2}, face={3}, loss={4}, dead={5} secs, rake={6}, rip={7}, thrash={8}",
                            target.HealthPercent,
                            target.Distance,
                            target.IsWithinMeleeRange.ToYN(),
                            Me.IsSafelyFacing(target).ToYN(),
                            target.InLineOfSpellSight.ToYN(),
                            target.TimeToDeath(),
                            (long)target.GetAuraTimeLeft("Rake", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Rip", true).TotalMilliseconds,
                            (long)target.GetAuraTimeLeft("Thrash", true).TotalMilliseconds
                            );
                    }

                    Logger.WriteDebug(Color.AntiqueWhite, log);
                    return RunStatus.Failure;
                })
                );
        }

        #endregion
    }
}