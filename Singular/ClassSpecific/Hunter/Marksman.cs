using System;
using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;

using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Hunter
{
    public class Marksman
    {
        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterMarksmanship,WoWContext.Normal)]
        public static Composite CreateMarksmanHunterNormalPullAndCombat()
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

                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Chimera Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent <= 90),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Steady Shot", ctx => StyxWoW.Me.HasAura("Steady Focus") && StyxWoW.Me.GetAuraTimeLeft("Steady Focus",true) < TimeSpan.FromSeconds(3)),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.HasAura("Master Marksman")),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent > 90 || StyxWoW.Me.HasAura("Rapid Fire") || PartyBuff.WeHaveBloodlust),
                Spell.Cast("Arcane Shot", ctx => (StyxWoW.Me.FocusPercent >= 66 || SpellManager.Spells["Chimera Shot"].CooldownTimeLeft >= TimeSpan.FromSeconds(5)) && (StyxWoW.Me.CurrentTarget.HealthPercent < 90 && !StyxWoW.Me.HasAura("Rapid Fire") && PartyBuff.WeHaveBloodlust)),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),

                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Battlegrounds)]
        public static Composite CreateMarksmanHunterPvPPullAndCombat()
        {
            return new PrioritySelector(
                Common.CreateHunterCallPetBehavior(false),

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
                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),

                // Rotation

                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Chimera Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent <= 90),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Steady Shot", ctx => StyxWoW.Me.HasAura("Steady Focus") && StyxWoW.Me.GetAuraTimeLeft("Steady Focus", true) < TimeSpan.FromSeconds(3)),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.HasAura("Master Marksman")),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent > 90 || StyxWoW.Me.HasAura("Rapid Fire") || PartyBuff.WeHaveBloodlust),
                Spell.Cast("Arcane Shot", ctx => (StyxWoW.Me.FocusPercent >= 66 || SpellManager.Spells["Chimera Shot"].CooldownTimeLeft >= TimeSpan.FromSeconds(5)) && (StyxWoW.Me.CurrentTarget.HealthPercent < 90 && !StyxWoW.Me.HasAura("Rapid Fire") && !PartyBuff.WeHaveBloodlust)),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),

                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship, WoWContext.Instances)]
        public static Composite CreateMarksmanHunterInstancePullAndCombat()
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

                Spell.Cast("Glaive Toss"),
                Spell.Cast("Powershot"),
                Spell.Cast("Barrage"),
                Spell.Cast("Blink Strike", ctx => StyxWoW.Me.GotAlivePet),
                Spell.Buff("Lynx Rush", ctx => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.Location.Distance(StyxWoW.Me.CurrentTarget.Location) < 10),
                Spell.Buff("Serpent Sting", ctx => !StyxWoW.Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Chimera Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent <= 90),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Rapid Fire", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) >= 2),
                Spell.Cast("Stampede"),
                Spell.Cast("Readiness", ctx => StyxWoW.Me.HasAura("Rapid Fire")),
                Spell.Cast("Steady Shot", ctx => StyxWoW.Me.HasAura("Steady Focus") && StyxWoW.Me.GetAuraTimeLeft("Steady Focus", true) < TimeSpan.FromSeconds(3)),
                Spell.Cast("Kill Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.HasAura("Master Marksman")),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Arcane Shot", ctx => StyxWoW.Me.HasAura("Thrill of the Hunt")),
                Spell.Cast("Aimed Shot", ctx => StyxWoW.Me.CurrentTarget.HealthPercent > 90 || StyxWoW.Me.HasAura("Rapid Fire") || PartyBuff.WeHaveBloodlust),
                Spell.Cast("Arcane Shot", ctx => (StyxWoW.Me.FocusPercent >= 66 || SpellManager.Spells["Chimera Shot"].CooldownTimeLeft >= TimeSpan.FromSeconds(5)) && (StyxWoW.Me.CurrentTarget.HealthPercent < 90 && !StyxWoW.Me.HasAura("Rapid Fire") && !PartyBuff.WeHaveBloodlust)),
                Spell.Cast("Fervor", ctx => StyxWoW.Me.FocusPercent <= 65 && StyxWoW.Me.HasAura("Frenzy") && StyxWoW.Me.Auras["Frenzy"].StackCount >= 5),

                Spell.Cast("Steady Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        private static uint _castId;
        private static int _steadyCount;
        private static bool _doubleSteadyCast;
        private static bool DoubleSteadyCast
        {
            get
            {
                if (_steadyCount > 1)
                {
                    _castId = 0;
                    _steadyCount = 0;
                    _doubleSteadyCast = false;
                    return _doubleSteadyCast;
                }
                
                return _doubleSteadyCast;
            }
            set 
            {
                if (_doubleSteadyCast && StyxWoW.Me.CurrentCastId == _castId)
                    return;

                _castId = StyxWoW.Me.CurrentCastId;
                _steadyCount++;
                _doubleSteadyCast = value;
            }
        }

        #endregion
    }
}
