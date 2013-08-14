using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx;
using System.Linq;
using Singular.Settings;

using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common.Helpers;

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }
        private static uint CurrentDemonicFury { get { return Me.GetCurrentPower(WoWPowerType.DemonicFury); } }

        private static int _mobCount;
        public static readonly WaitTimer demonFormRestTimer = new WaitTimer(TimeSpan.FromSeconds(3));


        #region Normal Rotation

        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDemonology, WoWContext.All)]
        public static Composite CreateWarlockDemonologyNormalCombat()
        {
            Kite.CreateKitingBehavior(CreateSlowMeleeBehavior(), null, null);

            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromLongRange(),

                Spell.WaitForCast(FaceDuring.Yes),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        // calculate key values
                        new Action(ret =>
                        {
                            Me.CurrentTarget.TimeToDeath();
                            _mobCount = Common.TargetsInCombat.Where(t => t.Distance <= (Me.MeleeDistance(t) + 3)).Count();
                            return RunStatus.Failure;
                        }),

                        CreateWarlockDiagnosticOutputBehavior(Dynamics.CompositeBuilder.CurrentBehaviorType.ToString()),

                        Helpers.Common.CreateAutoAttack(true),
                        new Decorator(
                            ret => Me.GotAlivePet && Me.GotTarget && Me.Pet.CurrentTarget != Me.CurrentTarget,
                            new Action(ret =>
                            {
                                PetManager.CastPetAction("Attack");
                                return RunStatus.Failure;
                            })
                            ),

                        Helpers.Common.CreateInterruptBehavior(),

                        // even though AOE spell, keep on CD for single target unless AoE turned off
                        new Decorator(
                            ret => Spell.UseAOE && Common.GetCurrentPet() == WarlockPet.Felguard,
                            new Sequence(
                                new PrioritySelector(
                                    Pet.CreateCastPetAction("Felstorm", ret => !Common.HasTalent(WarlockTalents.GrimoireOfSupremacy)),
                                    Pet.CreateCastPetAction("Wrathstorm", ret => Common.HasTalent(WarlockTalents.GrimoireOfSupremacy))
                                    ),
                                new ActionAlwaysFail()  // no GCD on Felstorm, allow to fall through
                                )
                            ),

                        new Decorator(
                            ret => MovementManager.IsClassMovementAllowed
                                && WarlockSettings.UseDemonicLeap
                                && ((Me.HealthPercent < 50 && SingularRoutine.CurrentWoWContext == WoWContext.Normal) || SingularRoutine.CurrentWoWContext == WoWContext.Battlegrounds)
                                && Unit.NearbyUnitsInCombatWithMeOrMyStuff.Any(u => u.IsWithinMeleeRange),
                            new PrioritySelector(
                                Spell.Cast("Carrion Swarm", req => Me.HasAura("Metamorphosis")),
                                Disengage.CreateDisengageBehavior("Demonic Leap", Disengage.Direction.Frontwards, 20, CreateSlowMeleeBehavior())
                                )
                            ),

            #region Felguard Use

 new Decorator(
                            ret => Common.GetCurrentPet() == WarlockPet.Felguard && Me.GotTarget && Me.CurrentTarget.Fleeing,
                            Pet.CreateCastPetAction("Axe Toss")
                            ),

            #endregion


            #region CurrentTarget DoTs

                // check two main DoTs so we cast based upon current state before we look at entering/leaving Metamorphosis
                        Spell.Cast("Corruption", req => !Me.HasAura("Metamorphosis") && Me.CurrentTarget.HasAuraExpired("Corruption", 3)),
                        new Throttle(1,
                            new Sequence(
                                Spell.CastHack("Metamorphosis: Doom", "Doom", on => Me.CurrentTarget, req => Me.HasAura("Metamorphosis") && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom", 10) && DoesCurrentTargetDeserveToGetDoom()),
                                new WaitContinue(TimeSpan.FromMilliseconds(350), canRun => Me.CurrentTarget.HasAura("Doom"), new ActionAlwaysSucceed())
                                )
                            ),

            #endregion

            #region Enter/Exit Metamorphosis based upon needs and fury levels

                // manage metamorphosis. don't use Spell.Cast family so we can manage the use of CanCast()
                        new Decorator(
                            ret => NeedToApplyMetamorphosis(),
                            new Sequence(
                                new Action(ret => Logger.Write(Color.White, "^Applying Metamorphosis Buff")),
                                new Action(ret => SpellManager.Cast("Metamorphosis", Me)),
                                new WaitContinue(
                                    TimeSpan.FromMilliseconds(450),
                                    canRun => Me.HasAura("Metamorphosis"),
                                    new Action(r =>
                                    {
                                        demonFormRestTimer.Reset();
                                        return RunStatus.Success;
                                    })
                                    )
                                )
                            ),

                        new Decorator(
                            ret => NeedToCancelMetamorphosis(),
                            new Sequence(
                                new Action(ret => Logger.Write(Color.White, "^Cancel Metamorphosis Buff")),
                // new Action(ret => Lua.DoString("CancelUnitBuff(\"player\",\"Metamorphosis\");")),
                                new Action(ret => Me.CancelAura("Metamorphosis")),
                                new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                                )
                            ),
            #endregion

            #region AOE

                // must appear after Mob count and Metamorphosis handling
                        CreateDemonologyAoeBehavior(),

            #endregion

            #region Single Target

                        new Throttle( TimeSpan.FromMilliseconds(2400), Spell.Cast("Soul Fire", mov => true, on => Me.CurrentTarget, req => Me.HasAura("Molten Core"), cancel => false)),

                        new Decorator(
                            ret => Me.HasAura("Metamorphosis"),
                            new PrioritySelector(
                                Spell.CastHack("Metamorphosis: Touch of Chaos", "Touch of Chaos", on => Me.CurrentTarget, req => true),
                                Spell.Cast("Soul Fire", ret => Me.Level < 25 /* dont know Touch of Chaos -or- Shadow Bolt */ ),
                                Spell.Cast("Shadow Bolt")
                                )
                            ),

                        new Decorator(
                            ret => !Me.HasAura("Metamorphosis"),
                            new PrioritySelector(
                                CreateHandOfGuldanBehavior(),
                                Spell.Cast("Shadow Bolt"),
                                Spell.Cast("Fel Flame", req => Me.IsMoving)
                                )
                            )

            #endregion
)
                    )
                );
        }

        private static uint endMoltenCore = 0;
        private static uint stackMoltenCore = 0;

        private static Composite CreateHandOfGuldanBehavior()
        {
            return new Throttle(
                TimeSpan.FromMilliseconds(500),
                new Decorator( 
                    ret => Me.CurrentTarget.HasAuraExpired("Hand of Gul'dan", "Shadowflame", 1),
                    new PrioritySelector(
                        Spell.CastOnGround("Hand of Gul'dan", loc => Me.CurrentTarget.Location, ret => TalentManager.HasGlyph("Hand of Gul'dan")),
                        Spell.Cast("Hand of Gul'dan", req => !TalentManager.HasGlyph("Hand of Gul'dan"))
                        )
                    )
                );
        }

        private static bool NeedToApplyMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCast = false;

            if (!hasAura && Me.GotTarget)
            {
                // check if we need Doom and have enough fury for 2 secs in form plus cast
                if (CurrentDemonicFury >= 72 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom") && DoesCurrentTargetDeserveToGetDoom())
                    shouldCast = true;
                // check if we have Corruption and we need to dump fury
                else if (CurrentDemonicFury >= WarlockSettings.FurySwitchToDemon && !Me.CurrentTarget.HasKnownAuraExpired("Corruption"))
                    shouldCast = true;
                
                // if we need to cast, check that we can
                if (shouldCast)
                    shouldCast = SpellManager.CanCast("Metamorphosis", Me, false);
            }

            return shouldCast;
        }

        private static bool DoesCurrentTargetDeserveToGetDoom()
        {
            if (SingularRoutine.CurrentWoWContext != WoWContext.Normal)
                return true;

            if ( Me.CurrentTarget.IsPlayer )
                return true;

            if ( Me.CurrentTarget.Elite && (Me.CurrentTarget.Level + 10) >= Me.Level )
                return true;

            return Me.CurrentTarget.TimeToDeath() > 45;
        }

        private static bool NeedToCancelMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCancel = false;

            if (hasAura && Me.GotTarget)
            {
                // switch back if not enough fury to cast anything (abc - always be casting)
                if (CurrentDemonicFury < 40)
                    shouldCancel = true;
                // check if we should stay in demon form because of buff
                else if (Me.HasAura("Dark Soul: Knowledge"))
                    shouldCancel = false;
                // check if we should stay in demon form because of Doom falling off
                else if ( CurrentDemonicFury >= 60 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom"))
                    shouldCancel = false;
                // finally... now check if we should cancel 
                else if ( CurrentDemonicFury < WarlockSettings.FurySwitchToCaster && Me.CurrentTarget.HasKnownAuraExpired("Corruption"))
                    shouldCancel = true;
                // do not need to check CanCast() on the cancel since we cancel the aura...
            }

            return shouldCancel;
        }

        #endregion

        #region AOE

        private static Composite CreateDemonologyAoeBehavior()
        {
            return new Decorator(
                ret => Spell.UseAOE,
                new PrioritySelector(
/*
                    new Decorator(
                        ret => Common.GetCurrentPet() == WarlockPet.Felguard && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.Pet.Location) < 8 * 8) > 1,
                        Pet.CreateCastPetAction("Felstorm")
                        ),
*/
                    new Decorator(
                        ret => Me.HasAura("Metamorphosis"),
                        new PrioritySelector(
                            Spell.Cast("Hellfire", ret => _mobCount >= 4 && SpellManager.HasSpell("Hellfire") && !Me.HasAura("Immolation Aura")),
                            new Decorator(
                                ret => _mobCount >= 2 && Common.TargetsInCombat.Count(t => !t.HasAuraExpired("Metamorphosis: Doom", "Doom", 1)) < Math.Min( _mobCount, 3),
                                Spell.CastHack( "Metamorphosis: Doom", "Doom", on => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Metamorphosis: Doom", "Doom", 1)), req => true)
                                )
                            )
                        ),

                    new Decorator(
                        ret => !Me.HasAura("Metamorphosis"),
                        new PrioritySelector(
                            new Decorator(
                                ret => _mobCount >= 2 && Common.TargetsInCombat.Count(t => !t.HasAuraExpired("Corruption")) < Math.Min( _mobCount, 3),
                                Spell.Cast("Corruption", ctx => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Corruption")))
                                )
                            )
                        )
                    )
                );
        }


        private static WoWUnit GetAoeDotTarget( string dotName)
        {
            WoWUnit unit = null;
            if (SpellManager.HasSpell(dotName))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasAuraExpired(dotName));

            return unit;
        }

        #endregion

        private static Composite CreateSlowMeleeBehavior()
        {
            return new PrioritySelector(
                ctx => SafeArea.NearestEnemyMobAttackingMe,
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Throttle(2,
                            new PrioritySelector(
                                Spell.CastHack("Metamorphosis: Chaos Wave", "Chaos Wave", on => Me.CurrentTarget, req => Me.HasAura("Metamorphosis")),
                                Spell.Buff("Shadowfury", onUnit => (WoWUnit)onUnit),
                                Spell.Buff("Howl of Terror", onUnit => (WoWUnit)onUnit),
                                new Decorator(
                                    ret => !Me.HasAura("Metamorphosis"),
                                    new PrioritySelector(
                                        Spell.Buff("Hand of Gul'dan", onUnit => (WoWUnit)onUnit, req => !TalentManager.HasGlyph("Hand of Gul'dan")),
                                        Spell.CastOnGround("Hand of Gul'dan", loc => ((WoWUnit)loc).Location, req => Me.GotTarget && TalentManager.HasGlyph("Hand of Gul'dan"), false)
                                        )
                                    ),
                                Spell.Buff("Mortal Coil", onUnit => (WoWUnit)onUnit),
                                Spell.Buff("Fear", onUnit => (WoWUnit)onUnit)
                                )
                            )
                        )
                    )
                );
        }

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

                        msg = string.Format(".... [{0}] h={1:F1}%/m={2:F1}%, fury={3}, metamor={4}, mcore={5}, darksoul={6}, aoecnt={7}",
                            s,
                             Me.HealthPercent,
                             Me.ManaPercent,
                             CurrentDemonicFury,
                             Me.HasAura("Metamorphosis"),
                             lstks,
                             Me.HasAura("Dark Soul: Knowledge"),
                             _mobCount
                             );

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