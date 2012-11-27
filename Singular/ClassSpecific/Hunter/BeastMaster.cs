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
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Hunter
{
    public class BeastMaster
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static HunterSettings HunterSettings { get { return SingularSettings.Instance.Hunter; } }

        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat,WoWClass.Hunter,WoWSpec.HunterBeastMastery,WoWContext.Normal | WoWContext.Instances )]
        public static Composite CreateBeastMasterHunterNormalPullAndCombat()
        {
            return new PrioritySelector(

                Safers.EnsureTarget(),

                //Common.CreateHunterBackPedal(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
            
                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Common.CreateHunterAvoidanceBehavior( null, null ),

                        Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),
                        
                        new Decorator(
                            ret => Me.CurrentTarget.Distance < 35f,
                            Movement.CreateEnsureMovementStoppedBehavior()
                            ),

                        Helpers.Common.CreateAutoAttack(true),

                        Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),

                        Spell.Buff("Concussive Shot",
                            ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid 
                                && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // Defensive Stuff
                        Spell.Cast( "Intimidation", 
                            ret => Me.GotTarget 
                                && Me.CurrentTarget.IsAlive 
                                && Me.GotAlivePet 
                                && (!Me.CurrentTarget.GotTarget || Me.CurrentTarget.CurrentTarget == Me)),

                        Common.CreateHunterTrapOnAddBehavior("Freeizng Trap"),

                        // AoE Rotation
                        new Decorator( 
                            ret => !(Me.CurrentTarget.IsBoss || Me.CurrentTarget.IsPlayer) && Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                            new PrioritySelector(
                                Spell.Cast( "Multi-Shot", ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where( u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f)),
                                Spell.Cast( "Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),
                                Spell.Cast( "Cobra Shot")
                                )
                            ),

                        // Single Target Rotation
                        Spell.Buff("Serpent Sting"),
                        Spell.Cast("Kill Command", ctx => Me.GotAlivePet && Me.Pet.GotTarget && Me.Pet.Location.Distance(Me.Pet.CurrentTarget.Location) < 25f),
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                        Spell.BuffSelf("Focus Fire", ctx => Me.HasAura("Frenzy", 5)),

                        Spell.Cast("Arcane Shot", ret => Me.FocusPercent > 60 || Me.HasAnyAura("Thrill of the Hunt", "The Beast Within")),
                        Spell.Cast("Cobra Shot")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery, WoWContext.Battlegrounds)]
        public static Composite CreateBeastMasterHunterPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),

                Common.CreateHunterAvoidanceBehavior(null, null),

                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => Me.CurrentTarget.Distance < 35f,
                    Movement.CreateEnsureMovementStoppedBehavior()),

                Spell.WaitForCast(true),
                Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                Helpers.Common.CreateAutoAttack(true),

                Common.CreateHunterTrapOnAddBehavior("Explosive Trap"),

                Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.HasAura("Enraged")),
                Spell.Buff("Concussive Shot",
                           ret =>
                           Me.CurrentTarget.CurrentTargetGuid == Me.Guid &&
                           Me.CurrentTarget.Distance > Spell.MeleeRange),

                // Defensive Stuff

                Spell.Cast(
                    "Intimidation", ret => Me.CurrentTarget.IsAlive && Me.GotAlivePet &&
                                           (Me.CurrentTarget.CurrentTarget == null ||
                                            Me.CurrentTarget.CurrentTarget == Me)),

                Spell.Cast("Mend Pet",
                           ret => Me.GotAlivePet && !Me.Pet.HasAura("Mend Pet") &&
                                  Me.Pet.HealthPercent < HunterSettings.MendPetPercent),

                Common.CreateHunterTrapBehavior("Snake Trap", false),
                Common.CreateHunterTrapBehavior("Immolation Trap", false),
                new Action(ctx =>
                               {
                                   var firstOrDefault = Me.CarriedItems.FirstOrDefault(ret => ret.Entry == 76089);
                                   if (firstOrDefault != null)
                                          firstOrDefault.UseContainerItem();
                               }),

                Spell.BuffSelf("Deterrence", ctx => Me.HealthPercent < 30),

                // Rotation

                Spell.BuffSelf("Focus Fire", ctx => Me.HasAura("Frenzy")),

                Spell.Buff("Serpent Sting", ctx => !Me.CurrentTarget.HasAura("Serpent Sting")),
                Spell.Cast("Fervor", ctx => Me.FocusPercent <= 65 && Me.HasAura("Frenzy") && Me.Auras["Frenzy"].StackCount >= 5),
                Spell.BuffSelf("Bestial Wrath", ctx => Me.FocusPercent > 60 && !Me.HasAura("The Beast Within")),
                Spell.Cast("Multi-Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.Cast("Steady Shot", ctx => Unit.NearbyUnfriendlyUnits.Count(u => u.IsTargetingMeOrPet) > 2),
                Spell.CastOnGround("Binding Shot", ret => Me.CurrentTarget.Location),
                Spell.CastOnGround("Flare", ret => Me.Location),
                Spell.Cast("Stampede"),
                Spell.Cast("Rapid Fire"),
                Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent < 20),
                Spell.Cast("Kill Command", ctx => Me.GotAlivePet && Me.Pet.Location.Distance(Me.CurrentTarget.Location) < Spell.MeleeRange),
                Spell.Buff("A Murder of Crows"),
                Spell.Cast("Glaive Toss"),
                Spell.Buff("Lynx Rush", ctx => Me.GotAlivePet && Me.Pet.Location.Distance(Me.CurrentTarget.Location) < 10),
                Spell.Cast("Dire Beast", ctx => Me.FocusPercent <= 90),
                Spell.Cast("Barrage"),
                Spell.Cast("Powershot"),
                Spell.Cast("Blink Strike", ctx => Me.GotAlivePet),
                Spell.Cast("Readiness", ctx => Me.HasAura("Rapid Fire")),
                Spell.Cast("Arcane Shot", ret => Me.FocusPercent > 60 || Me.HasAnyAura("Thrill of the Hunt", "The Beast Within")),
                Spell.Cast("Cobra Shot"),


                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

    }
}
