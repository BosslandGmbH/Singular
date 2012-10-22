using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Survival
    {
        #region Normal Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival, WoWContext.Normal)]
        public static Composite CreateHunterSurvivalNormalPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                               ret =>
                                SingularSettings.Instance.IsCombatRoutineMovementAllowed() &&
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

                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.HasAura("Locked and Load")),
                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Explosive Shot"),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Buff("Black Arrow", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Black Arrow")),
                Spell.Cast("Multi-Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 50 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 67),
                Spell.Cast("Steady Shot"),

              
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival, WoWContext.Battlegrounds)]
        public static Composite CreateHunterSurvivalPvPPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(false),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage", ret => SingularSettings.Instance.IsCombatRoutineMovementAllowed() && StyxWoW.Me.CurrentTarget.Distance < Spell.MeleeRange + 3f),
                Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                Spell.Cast("Tranquilizing Shot", ctx => StyxWoW.Me.CurrentTarget.HasAura("Enraged")),

                Spell.Cast("Concussive Shot", ret => StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid),
                Spell.Buff("Hunter's Mark"),
                Spell.BuffSelf("Aspect of the Hawk", ret => !StyxWoW.Me.HasAura("Aspect of the Iron Hawk") && !StyxWoW.Me.HasAura("Aspect of the Hawk")),
                // Defensive Stuff
                Spell.Cast(
                    "Intimidation", ret => StyxWoW.Me.CurrentTarget.IsAlive && StyxWoW.Me.GotAlivePet &&
                                           (StyxWoW.Me.CurrentTarget.CurrentTarget == null || StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me)),

                Common.CreateHunterTrapOnAddBehavior("Freezing Trap"),

                Spell.Cast("Mend Pet",
                    ret => StyxWoW.Me.GotAlivePet && !StyxWoW.Me.Pet.HasAura("Mend Pet") &&
                    (StyxWoW.Me.Pet.HealthPercent < SingularSettings.Instance.Hunter.MendPetPercent || (StyxWoW.Me.Pet.HappinessPercent < 90 && TalentManager.HasGlyph("Mend Pet")))),

                // Cooldowns
                Spell.BuffSelf("Rapid Fire",
                    ret => (StyxWoW.Me.HasAura("Call of the Wild") ||
                           !StyxWoW.Me.PetSpells.Any(s => s.Spell != null && s.Spell.Name == "Call of the Wild" && s.Spell.CooldownTimeLeft.TotalSeconds < 60)) &&
                           !StyxWoW.Me.HasAnyAura("Bloodlust", "Heroism", "Time Warp", "The Beast Within")),

                // Rotation
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.HasAura("Locked and Load")),
                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Explosive Shot"),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Buff("Black Arrow", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Black Arrow")),
                Spell.Cast("Multi-Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 50 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 67),
                Spell.CastOnGround("Flare", ret => StyxWoW.Me.Location),
                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),
                Spell.Cast("Steady Shot"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterSurvival, WoWContext.Instances)]
        public static Composite CreateHunterSurvivalInstancePullAndCombat()
        {
            return new PrioritySelector(
               Common.CreateHunterCallPetBehavior(true),

                Safers.EnsureTarget(),
                Spell.BuffSelf("Disengage",
                               ret =>
                                SingularSettings.Instance.IsCombatRoutineMovementAllowed() &&
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

                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.HasAura("Locked and Load")),
                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Explosive Shot"),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Buff("Black Arrow", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Black Arrow")),
                Spell.Cast("Multi-Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 50 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),
                Spell.Cast("Arcane Shot", ret => StyxWoW.Me.FocusPercent > 67),
                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion
    }
}
