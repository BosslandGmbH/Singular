using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.DeathKnight
{
    public class Blood
    {
        private static DeathKnightSettings Settings { get { return SingularSettings.Instance.DeathKnight(); } }
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Heal

        [Behavior(BehaviorType.Heal, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodHeals()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Death Pact",
                                ret =>
                                TalentManager.IsSelected((int) DeathKnightTalents.DeathPact) &&
                                StyxWoW.Me.HealthPercent < Settings.DeathPactPercent &&
                                Common.GhoulMinionIsActive),
                Spell.Cast("Death Siphon",
                            ret =>
                            TalentManager.IsSelected((int) DeathKnightTalents.DeathSiphon) &&
                            StyxWoW.Me.GotTarget &&
                            StyxWoW.Me.HealthPercent < Settings.DeathSiphonPercent),
                Spell.BuffSelf("Conversion",
                                ret =>
                                TalentManager.IsSelected((int) DeathKnightTalents.Conversion) &&
                                StyxWoW.Me.HealthPercent < Settings.ConversionPercent &&
                                StyxWoW.Me.RunicPowerPercent >=
                                Settings.MinimumConversionRunicPowerPrecent),

                Spell.BuffSelf("Rune Tap",
                                ret =>StyxWoW.Me.HealthPercent < Settings.RuneTapPercent || 
                                StyxWoW.Me.HealthPercent < 90 && StyxWoW.Me.HasAura("Will of the Necropolis")),

                Spell.BuffSelf("Death Coil",
                                ret =>
                                StyxWoW.Me.HealthPercent < Settings.LichbornePercent &&
                                StyxWoW.Me.HasAura("Lichborne")),

                Spell.BuffSelf("Lichborne",
                                ret =>
                                StyxWoW.Me.HealthPercent <
                                Settings.LichbornePercent
                                && StyxWoW.Me.CurrentRunicPower >= 60
                                && (!Settings.LichborneExclusive ||
                                    (!StyxWoW.Me.HasAura("Bone Shield")
                                        && !StyxWoW.Me.HasAura("Vampiric Blood")
                                        && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                                        && !StyxWoW.Me.HasAura("Icebound Fortitude")))),

                Spell.BuffSelf("Raise Dead",
                                ret =>
                                // I need to summon pet for Death Pact
                                StyxWoW.Me.HealthPercent < Settings.SummonGhoulPercentBlood &&
                                !Common.GhoulMinionIsActive &&
                                (!Settings.DeathPactExclusive ||
                                (!StyxWoW.Me.HasAura("Bone Shield")
                                    && !StyxWoW.Me.HasAura("Vampiric Blood")
                                    && !StyxWoW.Me.HasAura("Dancing Rune Weapon")
                                    && !StyxWoW.Me.HasAura("Lichborne")
                                    && !StyxWoW.Me.HasAura("Icebound Fortitude"))))
                );
        }

        #endregion


        #region CombatBuffs

        [Behavior(BehaviorType.CombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CreateDeathKnightBloodCombatBuffs()
        {
            return new PrioritySelector(
                // *** Defensive Cooldowns ***
                // Anti-magic shell - no cost and doesnt trigger GCD 
                Spell.BuffSelf("Anti-Magic Shell",
                                ret => Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                        (u.IsCasting || u.ChanneledCastingSpellId != 0) &&
                                                                        u.CurrentTargetGuid == StyxWoW.Me.Guid)),
                // we want to make sure our primary target is within melee range so we don't run outside of anti-magic zone.
                Spell.CastOnGround("Anti-Magic Zone", ctx => StyxWoW.Me.Location,
                                    ret => TalentManager.IsSelected((int) DeathKnightTalents.AntiMagicZone) &&
                                            !StyxWoW.Me.HasAura("Anti-Magic Shell") &&
                                            Unit.NearbyUnfriendlyUnits.Any(u =>
                                                                            (u.IsCasting ||
                                                                            u.ChanneledCastingSpellId != 0) &&
                                                                            u.CurrentTargetGuid == StyxWoW.Me.Guid) &&
                                            Targeting.Instance.FirstUnit != null &&
                                            Targeting.Instance.FirstUnit.IsWithinMeleeRange),

                Spell.Cast("Dancing Rune Weapon",
                    ret => Unit.NearbyUnfriendlyUnits.Count() > 2),

                Spell.BuffSelf("Bone Shield",
                    ret => !Settings.BoneShieldExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude")),

                Spell.BuffSelf("Vampiric Blood",
                    ret => Me.HealthPercent < Settings.VampiricBloodPercent
                        && (!Settings.VampiricBloodExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),

                Spell.BuffSelf("Icebound Fortitude",
                    ret => StyxWoW.Me.HealthPercent < Settings.IceboundFortitudePercent
                        && (!Settings.IceboundFortitudeExclusive || !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),

                Spell.BuffSelf("Lichborne",ret => StyxWoW.Me.IsCrowdControlled()),

                Spell.BuffSelf("Desecrated Ground", ret => Common.HasTalent( DeathKnightTalents.DesecratedGround) && StyxWoW.Me.IsCrowdControlled()),

                // Symbiosis
                Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < Settings.VampiricBloodPercent),

                // use army of the dead defensively
                Spell.BuffSelf("Army of the Dead",
                                ret =>
                                Settings.UseArmyOfTheDead &&
                                SingularRoutine.CurrentWoWContext == WoWContext.Instances &&
                                StyxWoW.Me.HealthPercent < Settings.ArmyOfTheDeadPercent),

                // I need to use Empower Rune Weapon to use Death Strike
                Spell.BuffSelf("Empower Rune Weapon",
                                ret =>
                                StyxWoW.Me.HealthPercent < Settings.EmpowerRuneWeaponPercent
                                && !SpellManager.CanCast("Death Strike")),

                new PrioritySelector(ctx => StyxWoW.Me.PartyMembers.FirstOrDefault(u => u.IsDead && u.DistanceSqr < 40 * 40 && u.InLineOfSpellSight),
                    Spell.Cast("Raise Ally", ctx => ctx as WoWUnit, ctx => ctx != null)
                    ),


                // *** Offensive Cooldowns ***
                // I am using pet as dps bonus
                Spell.BuffSelf("Raise Dead",
                                ret =>
                                Common.UseLongCoolDownAbility && Settings.UseGhoulAsDpsCdBlood &&
                                !Common.GhoulMinionIsActive),

                Spell.BuffSelf("Death's Advance",
                                ret =>
                                TalentManager.IsSelected((int) DeathKnightTalents.DeathsAdvance) &&
                                StyxWoW.Me.GotTarget && !SpellManager.CanCast("Death Grip", false) &&
                                StyxWoW.Me.CurrentTarget.DistanceSqr > 10*10),

                Spell.BuffSelf("Blood Tap",
                                ret =>
                                StyxWoW.Me.HasAura("Blood Charge") &&
                                StyxWoW.Me.Auras["Blood Charge"].StackCount >= 5 &&
                                (Common.BloodRuneSlotsActive == 0 || Common.FrostRuneSlotsActive == 0 ||
                                Common.UnholyRuneSlotsActive == 0)),

                Spell.Cast("Plague Leech", ret => Common.CanCastPlagueLeech)
                );
        }

        #endregion

        #region Normal Rotation

        private readonly static WaitTimer DeathStrikeTimer = new WaitTimer(TimeSpan.FromSeconds(5));
        private static List<WoWUnit> _nearbyUnfriendlyUnits;

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Normal)]
        public static Composite CreateDeathKnightBloodNormalCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(),
                    Helpers.Common.CreateAutoAttack(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(

                            Helpers.Common.CreateInterruptBehavior(),
                            Spell.BuffSelf("Blood Presence"),

                            Common.CreateDarkSuccorBehavior(),

                            Spell.Buff("Chains of Ice",
                                ret => StyxWoW.Me.CurrentTarget.Fleeing && !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                            Common.CreateDeathGripBehavior(),

                            // Start AoE section
                            new PrioritySelector(ctx => _nearbyUnfriendlyUnits =Unit.UnfriendlyUnitsNearTarget(15f).ToList(),
                                new Decorator(ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= Settings.DeathAndDecayCount,
                                    new PrioritySelector(
                                        Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),

                                        // Spell.Cast("Gorefiend's Grasp", ret => Common.HasTalent( DeathKnightTalents.GorefiendsGrasp)),
                                        Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),
                            // spread the disease around.
                                        Spell.BuffSelf("Unholy Blight",
                                                   ret => Spell.UseAOE &&
                                                   Common.HasTalent( DeathKnightTalents.UnholyBlight) &&
                                                   StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 &&
                                                   !StyxWoW.Me.HasAura("Unholy Blight")),

                                        Spell.Cast("Outbreak",
                                            ret => !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                (!StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                    !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague"))),

                                        Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                                        Spell.Buff("Plague Strike", true, "Blood Plague"),

                                        Spell.Cast("Blood Boil",
                                            ret => Common.HasTalent( DeathKnightTalents.RollingBlood) &&
                                                    !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                    StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 && Common.ShouldSpreadDiseases),

                                        Spell.Cast("Pestilence",
                                            ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),

                                        new Sequence(
                                            Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                            new Action(ret => DeathStrikeTimer.Reset())),
                                        Spell.Cast("Blood Boil", ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                                        Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                                        Spell.Cast("Rune Strike"),
                                        Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                        Movement.CreateMoveToMeleeBehavior(true)
                                        )
                                    )
                                ),

                            Spell.Cast("Outbreak",
                                ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                        !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                            Spell.Buff("Icy Touch", true,
                                        ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), 
                                        "Frost Fever"),
                            Spell.Buff("Plague Strike", true, "Blood Plague"),

                            // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                            Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                            Spell.Cast("Rune Strike"),

                            new Sequence(
                                Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                new Action(ret => DeathStrikeTimer.Reset())
                                ),
                            Spell.Cast("Blood Boil", ret => _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                            Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                            Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                            Spell.Cast("Blood Strike", ret => !SpellManager.HasSpell("Heart Strike") && _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                            Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),

                            // *** 3 Lowbie Cast what we have Priority
                            // ... not much to do here, just use our Unholy Runes on PS prior to learning DS
                            Spell.Cast("Plague Strike", ret => !SpellManager.HasSpell( "Death Strike"))
                            )
                        ),
  
                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Battlegrounds)]
        public static Composite CreateDeathKnightBloodPvPCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(),
                    Helpers.Common.CreateAutoAttack(true),
                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Helpers.Common.CreateInterruptBehavior(),
                            Spell.BuffSelf("Blood Presence"),
                            Common.CreateDeathGripBehavior(),
                            Spell.Buff("Chains of Ice",
                                ret => StyxWoW.Me.CurrentTarget.DistanceSqr > 10 * 10),

                            Common.CreateDarkSuccorBehavior(),

                        // Start AoE section
                        new PrioritySelector(ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(15f).ToList(),
                            new Decorator(ret => Spell.UseAOE && Unit.UnfriendlyUnitsNearTarget(15f).Count() >= Settings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),
                                    Spell.Cast("Gorefiend's Grasp", ret => Common.HasTalent( DeathKnightTalents.GorefiendsGrasp)),
                                    Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),
                        // spread the disease around.
                                    Spell.BuffSelf("Unholy Blight",
                                               ret =>
                                               Common.HasTalent( DeathKnightTalents.UnholyBlight) &&
                                               StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 &&
                                               !StyxWoW.Me.HasAura("Unholy Blight")),

                                    Spell.Cast("Outbreak",
                                        ret => !StyxWoW.Me.HasAura("Unholy Blight") &&
                                            (!StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague"))),

                                    Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                                    Spell.Buff("Plague Strike", true, "Blood Plague"),

                                    Spell.Cast("Blood Boil",
                                        ret => Common.HasTalent( DeathKnightTalents.RollingBlood) &&
                                                !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 && Common.ShouldSpreadDiseases),

                                    Spell.Cast("Pestilence",
                                        ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),
                                    Spell.Cast("Blood Boil", ret => _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                                    Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount)
                                    ))),

                            Spell.Cast("Outbreak",
                                ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                        !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                            Spell.Buff("Icy Touch", true, "Frost Fever"),
                            Spell.Buff("Plague Strike", true,"Blood Plague"),

                        // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                            Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                            Spell.Cast("Rune Strike"),
                            Spell.Cast("Death Coil", ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Death Coil", ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange ),
                            Spell.Buff("Necrotic Strike"),
                            new Sequence(
                                Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                new Action(ret => DeathStrikeTimer.Reset())),
                            Spell.Cast("Blood Boil", ret => _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                            Spell.Cast("Heart Strike", ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount),
                            Spell.Cast("Icy Touch")
                            )
                        ),

                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        #endregion

        #region Tanking - Instances and Raids

        // Blood DKs should be DG'ing everything it can when pulling. ONLY IN INSTANCES.
        [Behavior(BehaviorType.Pull, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Instances)]
        public static Composite CreateDeathKnightBloodInstancePull()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateDismount("Pulling"),
                    Spell.WaitForCast(),

                    Helpers.Common.CreateAutoAttack(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Spell.Cast("Outbreak"),
                            Spell.Cast("Icy Touch")
                            )
                        ),

                    Movement.CreateMoveToTargetBehavior(true, 5f)
                    );
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood, WoWContext.Instances)]
        public static Composite CreateDeathKnightBloodInstanceCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.WaitForCast(),
                    Helpers.Common.CreateAutoAttack(true),

                    new Decorator(
                        ret => !Spell.IsGlobalCooldown(),
                        new PrioritySelector(
                            Helpers.Common.CreateInterruptBehavior(),
                            Spell.BuffSelf("Blood Presence"),
                        // ** Cool Downs **
                            Spell.BuffSelf("Vampiric Blood",
                                           ret => StyxWoW.Me.HealthPercent <
                                                  Settings.VampiricBloodPercent),
                            Spell.BuffSelf("Bone Shield"),

                        // Taunts
                            new Decorator( 
                                ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Any(),

                                new Throttle( TimeSpan.FromSeconds(1),
                                    new PrioritySelector(
                                        Spell.Cast("Dark Command",
                                            ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                            ret => true),

                                        new Sequence(
                                            Spell.Cast("Death Grip",
                                                ret => TankManager.Instance.NeedToTaunt.FirstOrDefault(),
                                                ret => TankManager.Instance.NeedToTaunt.Any()   /*recheck just before referencing member*/
                                                    && TankManager.Instance.NeedToTaunt.FirstOrDefault().DistanceSqr > 10 * 10),
                                            new DecoratorContinue(
                                                ret => StyxWoW.Me.IsMoving,
                                                new Action(ret => Navigator.PlayerMover.MoveStop())),
                                            new WaitContinue(1, new ActionAlwaysSucceed())
                                            )
                                        )
                                    )
                                ),

                        // Start AoE section
                        new PrioritySelector(ctx => _nearbyUnfriendlyUnits = Unit.UnfriendlyUnitsNearTarget(15f).ToList(),
                            new Decorator(ret => Spell.UseAOE && _nearbyUnfriendlyUnits.Count() >= Settings.DeathAndDecayCount,
                                new PrioritySelector(
                                    Spell.CastOnGround("Death and Decay", ret => StyxWoW.Me.CurrentTarget.Location, ret => true, false),
                                    //Spell.Cast("Gorefiend's Grasp", ret => Common.HasTalent( DeathKnightTalents.GorefiendsGrasp)),
                                    Spell.Cast("Remorseless Winter", ret => Common.HasTalent( DeathKnightTalents.RemoreselessWinter)),
                        // spread the disease around.
                                    Spell.BuffSelf("Unholy Blight",
                                               ret =>
                                               Common.HasTalent( DeathKnightTalents.UnholyBlight) &&
                                               StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 &&
                                               !StyxWoW.Me.HasAura("Unholy Blight")),

                                    Spell.Cast("Outbreak",
                                        ret => !StyxWoW.Me.HasAura("Unholy Blight") &&
                                            (!StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                                !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague"))),

                                    Spell.Buff("Icy Touch", true, ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost), "Frost Fever"),
                                    Spell.Buff("Plague Strike", true, "Blood Plague"),

                                    Spell.Cast("Blood Boil",
                                        ret => Common.HasTalent( DeathKnightTalents.RollingBlood) &&
                                                !StyxWoW.Me.HasAura("Unholy Blight") &&
                                                StyxWoW.Me.CurrentTarget.DistanceSqr <= 10 * 10 && Common.ShouldSpreadDiseases),

                                    Spell.Cast("Pestilence",
                                        ret => !StyxWoW.Me.HasAura("Unholy Blight") && Common.ShouldSpreadDiseases),

                                    new Sequence(
                                        Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                        new Action(ret => DeathStrikeTimer.Reset())),
                                    Spell.Cast("Blood Boil", ret => _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                                    new Decorator(
                                        ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount,
                                        new PrioritySelector(
                                            Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                            Spell.Cast("Heart Strike")
                                            )
                                        ),
                                    Spell.Cast("Rune Strike"),
                                    Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                                    Movement.CreateMoveToMeleeBehavior(true)
                                    ))),
                            
                            Spell.Cast("Outbreak",
                                ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Frost Fever") ||
                                        !StyxWoW.Me.CurrentTarget.HasAura("Blood Plague")),
                            Spell.Buff("Icy Touch", true,ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost),"Frost Fever"),
                            Spell.Buff("Plague Strike", true,"Blood Plague"),
                        // If we don't have RS yet, just resort to DC. Its not the greatest, but oh well. Make sure we keep enough RP banked for a self-heal if need be.
                            Spell.Cast("Death Coil",
                                        ret => !SpellManager.HasSpell("Rune Strike") && StyxWoW.Me.CurrentRunicPower >= 80),
                            Spell.Cast("Death Coil",
                                        ret => !StyxWoW.Me.CurrentTarget.IsWithinMeleeRange),
                            Spell.Cast("Rune Strike"),
                            new Sequence(
                                Spell.Cast("Death Strike", ret => DeathStrikeTimer.IsFinished),
                                new Action(ret => DeathStrikeTimer.Reset())),
                            Spell.Cast("Blood Boil", ret => _nearbyUnfriendlyUnits.Count >= Settings.BloodBoilCount),
                            new Decorator(
                                ret => _nearbyUnfriendlyUnits.Count < Settings.BloodBoilCount,
                                new PrioritySelector(
                                    Spell.Cast("Soul Reaper", ret => StyxWoW.Me.CurrentTarget.HealthPercent < 35),
                                    Spell.Cast("Heart Strike")
                                    )
                                ),
                            Spell.Cast("Icy Touch", ret => !StyxWoW.Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost))
                            )
                        ),

                    Movement.CreateMoveToMeleeBehavior(true)
                    );
        }

        #endregion
    }
}
