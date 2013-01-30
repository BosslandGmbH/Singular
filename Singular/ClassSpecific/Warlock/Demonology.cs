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

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock(); } }
        private static uint CurrentDemonicFury { get { return Me.GetCurrentPower(WoWPowerType.DemonicFury); } }

        private static int _mobCount; 

        #region Normal Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDemonology, WoWContext.All)]
        public static Composite CreateWarlockDemonologyNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Spell.WaitForCast(true),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Action(ret =>
                        {
                            _mobCount = Common.TargetsInCombat.Count();
                            return RunStatus.Failure;
                        }),
                        new Action( ret => {
                            _mobCount = Common.TargetsInCombat.Where(t=>t.Distance <= (Me.MeleeDistance(t) + 3)).Count();
                            return RunStatus.Failure;
                            }),

                        CreateWarlockDiagnosticOutputBehavior(),

                        //Helpers.Common.CreateAutoAttack(true),
                        new Decorator(
                            ret => Me.GotAlivePet && Me.GotTarget && Me.Pet.CurrentTarget != Me.CurrentTarget,
                            new Action( ret => {
                                PetManager.CastPetAction("Attack");
                                return RunStatus.Failure;
                                })
                            ),

                        Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

                        // even though AOE spell, keep on CD for single target unless AoE turned off
                        new Decorator(
                            ret => Spell.UseAOE && Common.GetCurrentPet() == WarlockPet.Felguard,
                            Pet.CreateCastPetAction("Felstorm")
                            ),


            #region Felguard Use

                        new Decorator(
                            ret => Common.GetCurrentPet() == WarlockPet.Felguard && Me.CurrentTarget.Fleeing,
                            Pet.CreateCastPetAction("Axe Toss")
                            ),

            #endregion

            #region Apply Metamorphosis

                // manage metamorphosis. don't use Spell.Cast family so we can manage the use of CanCast()
                        new Decorator(
                            ret => NeedToApplyMetamorphosis(),
                            new Sequence(
                                new Action( ret => Logger.Write( Color.White, "^Applying Metamorphosis Buff")),
                                new Action( ret => SpellManager.Cast("Metamorphosis", Me)),
                                new WaitContinue( TimeSpan.FromMilliseconds(450), canRun => Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                                )
                            ),

                        new Decorator(
                            ret => NeedToCancelMetamorphosis(),
                            new Sequence(
                                new Action(ret => Logger.Write( Color.White, "^Cancel Metamorphosis Buff")),
                                // new Action(ret => Lua.DoString("CancelUnitBuff(\"player\",\"Metamorphosis\");")),
                                new Action( ret => Me.CancelAura( "Metamorphosis")),
                                new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                                )
                            ),
            #endregion

            #region AOE

                        // must appear after Mob count and Metamorphosis handling
                        CreateDemonologyAoeBehavior(),

            #endregion

            #region Single Target

                        new Decorator(
                            ret => Me.HasAura( "Metamorphosis"),
                            new PrioritySelector(
                                new Sequence(
                                    CastHack( "Metamorphosis: Doom", "Doom", on => Me.CurrentTarget, req => Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom", 10)),
                                    new WaitContinue(TimeSpan.FromMilliseconds(250), canRun => Me.CurrentTarget.HasAura("Doom"), new ActionAlwaysSucceed())
                                    ),
                                CastHack("Metamorphosis: Touch of Chaos", "Touch of Chaos", on => Me.CurrentTarget, req => true)
                                )
                            ),

                        new Decorator(
                            ret => !Me.HasAura( "Metamorphosis"),
                            new PrioritySelector(
                                Spell.Cast("Corruption", req => Me.CurrentTarget.HasAuraExpired("Corruption", 3)),
                                CreateHandOfGuldanBehavior(),
                                Spell.Cast("Soul Fire", ret => Me.HasAura("Molten Core")),
                                Spell.Cast("Shadow Bolt")
                                )
                            )

            #endregion
                        )
                    ),

                Movement.CreateMoveToRangeAndStopBehavior( toUnit => Me.CurrentTarget, range => 35f)
                );
        }

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
                if (CurrentDemonicFury >= 72 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom"))
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

        private static bool NeedToCancelMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCancel = false;

            if (hasAura && Me.GotTarget)
            {
                if (CurrentDemonicFury < 40)
                    shouldCancel = true;
                // check if we should stay in demon form because of buff (only if we have enough fury for a cast)
                if (Me.HasAura("Dark Soul: Knowledge"))
                    shouldCancel = false;
                // check if we should stay in demon form because of Doom falling off
                else if ( CurrentDemonicFury >= 60 && Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom"))
                    shouldCancel = false;
                // finally... now check if we should cancel 
                else if ( CurrentDemonicFury < WarlockSettings.FurySwitchToCaster && Me.CurrentTarget.HasKnownAuraExpired("Corruption"))
                    shouldCancel = true;
                // do not need to check CanCast() on the cancel ...
            }

            return shouldCancel;
        }

        // following done because CanCast() wants spell as "Metamorphosis: Doom" while Cast() and aura name are "Doom"
        public static Composite CastHack(string canCastName, string castName, UnitSelectionDelegate onUnit, SimpleBooleanDelegate requirements)
        {
            return new Decorator(ret => castName != null && requirements != null && onUnit != null && onUnit(ret) != null && requirements(ret) && SpellManager.CanCast(canCastName, onUnit(ret), true, false),
                new Throttle(
                    new Action(ret =>
                    {
                        Logger.Write(string.Format("Casting {0} on {1}", castName, onUnit(ret).SafeName()));
                        SpellManager.Cast(castName, onUnit(ret));
                    })
                    )
                );
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
                                CastHack( "Metamorphosis: Doom", "Doom", on => Common.TargetsInCombat.FirstOrDefault(m => m.HasAuraExpired("Metamorphosis: Doom", "Doom", 1)), req => true)
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

        private static Composite CreateWarlockDiagnosticOutputBehavior()
        {
            return new Throttle(1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                    {
                        WoWUnit target = Me.CurrentTarget;
                        uint lstks = !Me.HasAura("Molten Core") ? 0 : Me.ActiveAuras["Molten Core"].StackCount;

                        string msg;
                        
                        msg = string.Format(".... h={0:F1}%/m={1:F1}%, fury={2}, metamor={3}, mcore={4}, darksoul={5}, aoecnt={6}",
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
                            msg += string.Format(", enemy={0}% @ {1:F1} yds, corrupt={2}, doom={3}, shdwflm={4}",
                                (int)target.HealthPercent,
                                target.Distance,
                                (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Doom", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Shadowflame", true).TotalMilliseconds
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