
using System.Linq;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Pathing;

using TreeSharp;
using CommonBehaviors.Actions;
using Styx.Logic.Combat;
using Styx.WoWInternals.WoWObjects;


namespace Singular
{
    partial class SingularRoutine
    {

        private bool movingAway = false;
        
        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateFireMagePVPCombat()
        {
            return 
                new PrioritySelector(
                    new Decorator(ret => Me.HasAura("Ice Block") || Me.HasAura("Cyclone"),
                        new Action(ret => { return RunStatus.Success; })),
                    CreateCheckPlayerPvPState(),
                    CreateEnsurePVPTargetRanged(),
                    new Decorator(ret => Me.CurrentTarget == null,
                        new Action(ret => { return RunStatus.Success; })),
                    CreateCheckTargetPvPState(),
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
					CreateWaitForCast(true),
                    CreateSpellCast("Escape Artist", ret => MeRooted),
                    CreateSpellCast("Blink", ret => MeUnderStunLikeControl || MeRooted),
                    new Decorator( 
                        ret => MeUnderStunLikeControl ||
                               MeUnderSheepLikeControl ||
                               MeUnderFearLikeControl,
                        new PrioritySelector(
                               CreateUseAntiPvPControl(),
                               CreateSpellCast("Ice Block"),
                               new Action(ret => { return RunStatus.Success; }))),
                    CreateSpellCast("Counterspell", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != 0),
                    new Action(ret => 
                        {
                            // clear I"m moving away flag
                            if (!Me.IsMoving && movingAway)
                                movingAway = false;
                            return RunStatus.Failure;
                        }),
                    new Decorator(ret => SpellManager.CanCast("Counterspell"),
                        new Action(ret =>
                            {
                                if (unitsAt40Range == null)
                                    return RunStatus.Failure;
                                
                                WoWPlayer player = unitsAt40Range.FirstOrDefault(
                                    p => p.DistanceSqr < 30 * 30 && (p.IsCasting || p.ChanneledCastingSpellId != 0) &&
                                        p.IsTargetingMeOrPet);

                                if (player != null)
                                {
                                    if (Me.IsCasting)
                                        SpellManager.StopCasting();

                                    SpellManager.Cast("Counterspell", player);
                                    return RunStatus.Success;
                                }

                                return RunStatus.Failure;
                            })),
                    new Decorator(
                        ret => !movingAway,
                        CreateMoveToAndFacePvP(40f, 70f, ret => Me.CurrentTarget, false, RunStatus.Success)),
                    new Decorator(ret => unitsAt40Range != null &&
                        unitsAt40Range.Count(a => a.DistanceSqr < 15 * 15) > 1 && SpellManager.CanCast("Blast Wave"),
                        new Action(ret =>
                        {
                            SpellManager.Cast("Blast Wave");
                            LegacySpellManager.ClickRemoteLocation(unitsAt40Range.First(a => a.DistanceSqr < 15 * 15).Location);
                        })),
                    new Action(ret =>
                        {
                            if (SpellManager.CanCast("Dragon's Breath") && unitsAt40Range != null)
                            {
                                WoWPlayer unit = unitsAt40Range.FirstOrDefault(u => u.DistanceSqr < 8 * 8);
                                if (unit != null)
                                {
                                    Navigator.PlayerMover.MoveStop();
                                    unit.Face();
                                    StyxWoW.SleepForLagDuration();
                                    SpellManager.Cast("Dragon's Breath");
                                    StyxWoW.SleepForLagDuration();

                                    WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, unit.Location, 10f);

                                    if (Navigator.CanNavigateFully(Me.Location, moveTo))
                                    {
                                        movingAway = true;
                                        Navigator.MoveTo(moveTo);
                                    }

                                    return RunStatus.Success;
                                }
                            }

                            return RunStatus.Failure;
                        }),
                    CreateSpellCast("Pyroblast",
                        ret => Me.HasAura("Hot Streak") &&
                               Me.Auras["Hot Streak"].TimeLeft.TotalSeconds > 1 &&
                               !TargetState.ReflectMagic &&
                               !TargetState.Invulnerable,
                               false),
                    CreateSpellBuff("Living Bomb", ret => (!Me.CurrentTarget.HasAura("Living Bomb") ||
                        Me.CurrentTarget.Auras["Living Bomb"].CreatorGuid != Me.Guid) &&
                        !TargetState.Invulnerable &&
                        !TargetState.ResistsBinarySpells &&
                        !TargetState.ReflectMagic,
                        false),
                    CreateSpellCast("Fire Blast", ret => Me.HasAura("Impact") && !TargetState.ReflectMagic, false),
                    CreateSpellCast("Scorch", ret => Me.IsMoving, false),
                    new Decorator(
                        ret => (TargetState.Rooted  || TargetState.Stunned || TargetState.Feared | TargetState.Incapacitated) && 
                            Me.CurrentTarget.DistanceSqr < 10 * 10,
                        new Action(
                            ret =>
                            {
                                Logger.Write("Getting away from frozen target");
                                WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(Me.Location, Me.CurrentTarget.Location, 10f);

                                if (Navigator.CanNavigateFully(Me.Location, moveTo))
                                {
                                    movingAway = true;
                                    Navigator.MoveTo(moveTo);
                                }
                            })),
                    CreateSpellCast("Frost Nova", ret => unitsAt40Range.Count(p => p.DistanceSqr < 8 * 8) > 0),
                    CreateSpellCast("Cone of Cold", ret => Me.CurrentTarget.DistanceSqr <= (8 * 8) && 
                        !TargetState.Freezed && !TargetState.Slowed && !TargetState.Stunned),
                    new Action(ret =>
                        {
                            // check if target is invulnerable, try to get new target
                            if (!TargetState.Invulnerable)
                                return RunStatus.Failure;

                            if (unitsAt40Range != null)
                            {
                                WoWPlayer player = unitsAt40Range.OrderBy(p => p.DistanceSqr).FirstOrDefault(p => p != Me.CurrentTarget);
                                if (player != null)
                                {
                                    player.Target();
                                    return RunStatus.Success;
                                }
                            }

                            return RunStatus.Failure;
                        }),
                    CreateSpellCast("Evocation", ret => Me.ManaPercent < 40),
                    CreateSpellCast("Fireball", ret => unitsAt40Range == null ||
                        unitsAt40Range.Count(a => a.DistanceSqr < 10 * 10) == 0 && 
                        Me.CurrentTarget.HasAura("Critical Mass") &&
                        !TargetState.ReflectMagic),
                    CreateSpellCast("Scorch", false)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.Battlegrounds)]
        public Composite CreateFireMagePVPPull()
        {
            return
                new PrioritySelector(
                    CreateEnsurePVPTargetRanged(),
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(40f, ret => Me.CurrentTarget),
                    CreateSpellBuffOnSelf("Mana Shield"), 
                    CreateSpellBuff("Living Bomb", ret => !Me.CurrentTarget.HasAura("Living Bomb")),
                    CreateSpellCast("Scorch"));

        }
    }
}
