using System;
using Singular.Dynamics;
using Singular.Helpers;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx;
using System.Linq;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }

        private static int _mobCount;
        public static readonly WaitTimer demonFormRestTimer = new WaitTimer(TimeSpan.FromSeconds(3));

        private static uint SoulShardCount {  get { return Me.GetPowerInfo(WoWPowerType.SoulShards).Current; } }

        private static DateTime _lastSoulFire = DateTime.MinValue;

        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDemonology)]
        public static Composite CreateWarlockDemonologyNormalCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // calculate key values
                        new Action(ret =>
                        {
                            Me.CurrentTarget.TimeToDeath();
                            _mobCount = Common.TargetsInCombat.Count(t => t.Distance <= Me.MeleeDistance(t) + 3);
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior(CompositeBuilder.CurrentBehaviorType.ToString()),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        // even though AOE spell, keep on CD for single target unless AoE turned off
                        new Decorator(
                            ret => Spell.UseAOE && WarlockSettings.FelstormMobCount > 0 && Common.GetCurrentPet() == WarlockPet.Felguard && !Spell.IsSpellOnCooldown("Command Demon") && Me.Pet.GotTarget(),
                            new Sequence(
                                new PrioritySelector(
                                    ctx =>
                                    {
                                        int mobCount = Unit.UnfriendlyUnits().Count(t => Me.Pet.SpellDistance(t) < 8f);
                                        if (mobCount > 0)
                                        {
                                            // try not to waste Felstorm if mob will die soon anyway
                                            if (mobCount == 1)
                                            {
                                                if (Me.Pet.CurrentTargetGuid == Me.CurrentTargetGuid && !Me.CurrentTarget.IsPlayer && Me.CurrentTarget.TimeToDeath() < 6)
                                                {
                                                    Logger.WriteDebug("Felstorm: found {0} mobs within 8 yds of Pet, but saving Felstorm since it will die soon", mobCount, !Me.Pet.GotTarget() ? -1f : Me.Pet.SpellDistance(Me.Pet.CurrentTarget));
                                                    return 0;
                                                }
                                            }

                                            if (SingularSettings.Debug)
                                                Logger.WriteDebug("Felstorm: found {0} mobs within 8 yds of Pet; Pet is {1:F1} yds from its target", mobCount, !Me.Pet.GotTarget() ? -1f : Me.Pet.SpellDistance(Me.Pet.CurrentTarget));
                                        }
                                        return mobCount;
                                    },
                                    Spell.Cast("Wrathstorm", req => ((int)req) >= WarlockSettings.FelstormMobCount),
                                    Spell.Cast("Felstorm", req => ((int)req) >= WarlockSettings.FelstormMobCount)
                /*
                                                    ,
                                                    new Decorator(
                                                        req => Me.Pet.GotTarget() && !Me.Pet.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModHealingReceived),
                                                        new PrioritySelector(
                                                            Spell.Cast( "Mortal Cleave", req => (int)req < WarlockSettings.FelstormMobCount || Spell.IsSpellOnCooldown("Felstorm")),
                                                            Spell.Cast( "Legion Strike", req => (int) req < WarlockSettings.FelstormMobCount || Spell.IsSpellOnCooldown("Felstorm"))
                                                            )
                                                        )
                */
                                    ),
                                new ActionAlwaysFail()  // no GCD on Felstorm, allow to fall through
                                )
                            ),

            #region Felguard Use

                        new ThrottlePasses(
                            1,
                            TimeSpan.FromSeconds(0.5),
                            RunStatus.Failure,
                            Spell.HandleOffGCD(
                                new Decorator(
                                    ret => Me.GotTarget()
                                        && Common.GetCurrentPet() == WarlockPet.Felguard
                                        && Me.CurrentTarget.SpellDistance() < 30
                                        && ((WarlockSettings.StunWhileSolo && Me.CurrentTarget.CurrentTargetGuid == Me.Guid) || Me.CurrentTarget.IsPlayer || Me.CurrentTarget.IsMovingAway()) 
                                        && !Me.CurrentTarget.IsCrowdControlled(),
                                    Pet.CastPetAction("Axe Toss")
                                    )
                                )
                            ),

            #endregion


            #region CurrentTarget DoTs

                        Common.CastCataclysm(),


                        // Artifact Weapon
                        new Decorator(
                            ret => WarlockSettings.UseArtifactOnlyInAoE && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Count() > 1,
                            new PrioritySelector(
                                Spell.Cast("Thal'kiel's Consumption",
                                    ret =>
                                        WarlockSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None
                                        && Me.Minions.Count() >= WarlockSettings.ArtifactDemonCount)
                            )
                        ),
                        Spell.Cast("Thal'kiel's Consumption",
                            ret =>
                                !WarlockSettings.UseArtifactOnlyInAoE
                                && WarlockSettings.UseDPSArtifactWeaponWhen != UseDPSArtifactWeaponWhen.None
                                && Me.Minions.Count() >= WarlockSettings.ArtifactDemonCount
                        ),

                        Spell.Buff("Doom", req => !Me.CurrentTarget.HasAura("Doom")),
                        Spell.Cast("Summon Darkglare"),
                        Spell.Cast("Call Dreadstalkers"),
                        Spell.Cast("Summon Doomguard"),
                        Spell.Cast("Hand of Gul'dan", movement => false, target => Me.CurrentTarget, req => SoulShardCount >= 4),
                        Spell.Cast("Grimoire: Felguard"),
                        Spell.Cast("Demonic Empowerment", req => Me.Minions.Count(min => !min.HasAura(193396)) >= 3 && Spell.LastSpellCast != "Demonic Empowerment"),
                        Spell.Cast("Soul Harvest"),
                        Spell.Cast("Command Demon"),
                        Spell.Cast("Felstorm"),
                        Spell.Buff("Life Tap", req => Me.ManaPercent <= 60),
                        Spell.Cast("Demonbolt"),
                        Spell.Cast("Shadow Bolt"),

            #endregion

            #region Single Target

                        // when 2 stacks present, don't throttle cast
                        new Sequence(
                            ctx =>
                            {
                                uint stacks = Me.GetAuraStacks("Molten Core");
                                if (stacks > 0 && (DateTime.UtcNow - _lastSoulFire).TotalMilliseconds < 250)
                                    stacks--;
                                return stacks;
                            },
                            Spell.Cast("Soul Fire", mov => true, on => Me.CurrentTarget, req => ((uint)req) > 0, cancel => false),
                            new Action(r => _lastSoulFire = DateTime.UtcNow)
                            )
                        
            #endregion
)
                    )
                );
        }
        
        #endregion

        private static Composite CreateWarlockDiagnosticOutputBehavior(string s)
        {
            return new ThrottlePasses(1, 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;
                        uint lstks = !Me.HasAura("Molten Core") ? 0 : Me.ActiveAuras["Molten Core"].StackCount;

                        string msg;

                        msg =
	                        $".... [{s}] h={Me.HealthPercent:F1}%/m={Me.ManaPercent:F1}%, metamor={Me.HasAura("Metamorphosis")}, mcore={lstks}, darksoul={Me.HasAura("Dark Soul: Knowledge")}, aoecnt={_mobCount}";

                        if (target != null)
                        {
                            msg += string.Format(", enemy={0}% @ {1:F1} yds, face={2}, loss={3}, corrupt={4}, doom={5}, shdwflm={6}, ttd={7}",
                                (int)target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Doom", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Shadowflame", true).TotalMilliseconds,
                                target.TimeToDeath()
                                );
                        }

                        Logger.WriteDebug(Color.Wheat, msg);
                        return RunStatus.Failure;
                    })
                )
            );
        }

    }
}