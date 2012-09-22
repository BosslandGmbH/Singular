using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class BeastMaster
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterBeastMastery,WoWContext.Normal)]
        public static Composite CreateBeastMasterHunterNormalPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                               ret =>
                               SingularSettings.Instance.Hunter.UseDisengage &&
                               StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                //Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.BuffSelf("Aspect of the Fox", ret => StyxWoW.Me.IsMoving),
                Spell.BuffSelf("Aspect of the Hawk",
                               ret => !StyxWoW.Me.IsMoving &&
                               !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") &&
                               !StyxWoW.Me.HasAura("Aspect of the Hawk")),

                Helpers.Common.CreateAutoAttack(true),

                Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),
                Spell.Buff("Concussive Shot",
                           ret =>
                           StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid &&
                           StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange),
                Spell.Buff("Hunter's Mark", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Hunter's Mark")),

                // Defensive Stuff

                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null ||
                                            StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),

                Spell.Cast("Mend Pet",
                           ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                                  (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent ||
                                   (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                Common.CreateHunterTrapOnAddBehavior("Freezing Trap"),

                // Rotation

                Spell.BuffSelf("Focus Fire", ctx => StyxWoW.Me.HasAura("Frenzy")),

                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.BuffSelf("Bestial Wrath", ctx => StyxWoW.Me.FocusPercent > 60 && !StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Kill Command", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Glaive Toss"),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Dire Beast", ctx => StyxWoW.Me.FocusPercent <= 90),
                Spell.Cast("Barrage"),
                Spell.Cast("Powershot"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 60 || StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery, WoWContext.Battlegrounds)]
        public static Composite CreateBeastMasterHunterPvPPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                               ret =>
                               SingularSettings.Instance.Hunter.UseDisengage &&
                               StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f && Navigator.CanNavigateFully(StyxWoW.Me.Location,WoWMathHelper.CalculatePointBehind(StyxWoW.Me.Location,StyxWoW.Me.Rotation, 30))),
                //Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.BuffSelf("Aspect of the Fox", ret => StyxWoW.Me.IsMoving),
                Spell.BuffSelf("Aspect of the Hawk",
                               ret => !StyxWoW.Me.IsMoving &&
                               !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") &&
                               !StyxWoW.Me.HasAura("Aspect of the Hawk")),

                Helpers.Common.CreateAutoAttack(true),

                Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),
                Spell.Buff("Concussive Shot",
                           ret =>
                           StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid &&
                           StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange),
                Spell.Buff("Hunter's Mark", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Hunter's Mark")),

                // Defensive Stuff

                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null ||
                                            StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),

                Spell.Cast("Mend Pet",
                           ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                                  (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent ||
                                   (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),
                new Action(ctx => StyxWoW.Me.CarriedItems.FirstOrDefault(ret => ret.Entry == 76089).UseContainerItem()),

                Spell.BuffSelf("Deterrence", ctx => StyxWoW.Me.HealthPercent < 30),

                // Rotation

                Spell.BuffSelf("Focus Fire", ctx => StyxWoW.Me.HasAura("Frenzy")),

                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.BuffSelf("Bestial Wrath", ctx => StyxWoW.Me.FocusPercent > 60 && !StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.CastOnGround("Flare", ret => StyxWoW.Me.Location),
                Spell.Cast("Stampede"),
                Spell.Cast("Rapid Fire"),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Kill Command", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Glaive Toss"),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Dire Beast", ctx => StyxWoW.Me.FocusPercent <= 90),
                Spell.Cast("Barrage"),
                Spell.Cast("Powershot"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 60 || StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery, WoWContext.Instances)]
        public static Composite CreateBeastMasterHunterInstancePullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                               ret =>
                               SingularSettings.Instance.Hunter.UseDisengage &&
                               StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                //Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.BuffSelf("Aspect of the Fox", ret => StyxWoW.Me.IsMoving),
                Spell.BuffSelf("Aspect of the Hawk",
                               ret => !StyxWoW.Me.IsMoving &&
                               !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") &&
                               !StyxWoW.Me.HasAura("Aspect of the Hawk")),

                Helpers.Common.CreateAutoAttack(true),

                Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),
                Spell.Buff("Concussive Shot",
                           ret =>
                           StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid &&
                           StyxWoW.Me.CurrentTarget.Distance > Spell.MeleeRange),
                Spell.Buff("Hunter's Mark", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Hunter's Mark")),

                // Defensive Stuff

                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null ||
                                            StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),

                Spell.Cast("Mend Pet",
                           ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                                  (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent ||
                                   (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                Common.CreateHunterTrapOnAddBehavior("Freezing Trap"),

                // Rotation

                Spell.BuffSelf("Focus Fire", ctx => StyxWoW.Me.HasAura("Frenzy")),

                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.BuffSelf("Bestial Wrath", ctx => StyxWoW.Me.FocusPercent > 60 && !StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Kill Command", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Glaive Toss"),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Dire Beast", ctx => StyxWoW.Me.FocusPercent <= 90),
                Spell.Cast("Barrage"),
                Spell.Cast("Powershot"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 60 || StyxWoW.Me.HasAura("The Beast Within")),
                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
