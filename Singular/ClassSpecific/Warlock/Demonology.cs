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
        private static WarlockSettings WarlockSettings { get { return SingularSettings.Instance.Warlock; } }
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
                Spell.WaitForCast(true, false),

                new Decorator(ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateWarlockDiagnosticOutputBehavior(),

                        //Helpers.Common.CreateAutoAttack(true),
                        new Decorator(
                            ret => Me.GotAlivePet && (Me.Pet.CurrentTarget == null || StyxWoW.Me.Pet.CurrentTarget != StyxWoW.Me.CurrentTarget),
                            new Action( ret => {
                                PetManager.CastPetAction("Attack");
                                return RunStatus.Failure;
                                })
                            ),


                        Helpers.Common.CreateInterruptSpellCast(ret => Me.CurrentTarget),

            #region Felguard Use

                        new Decorator(
                            ret => Common.GetCurrentPet() == WarlockPet.Felguard,
                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.CurrentTarget.Fleeing,
                                    Pet.CreateCastPetAction("Axe Toss")),

                                new Decorator(
                                    ret => Me.GotAlivePet && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(Me.Pet.Location) < 10 * 10) > 1,
                                    Pet.CreateCastPetAction("Felstorm"))
                                )
                            ),

                        new Action( ret => {
                            _mobCount = Common.TargetsInCombat.Where(t=>t.Distance <= (Me.MeleeDistance(t) + 3)).Count();
                            return RunStatus.Failure;
                            }),

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
                                new Action(ret => SpellManager.Cast("Metamorphosis", Me.CurrentTarget)),
                                new WaitContinue(TimeSpan.FromMilliseconds(450), canRun => !Me.HasAura("Metamorphosis"), new ActionAlwaysSucceed())
                                )
                            ),
            #endregion

            #region AOE


            #endregion

            #region Single Target

                        new Decorator(
                            ret => Me.HasAura( "Metamorphosis"),
                            new PrioritySelector(
                                CastHack( "Metamorphosis: Doom", "Doom", on => Me.CurrentTarget, req => Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom", 5)),
                                CastHack("Metamorphosis: Touch of Chaos", "Touch of Chaos", on => Me.CurrentTarget, req => Me.HasAura("Dark Soul: Knowledge") || !Me.CurrentTarget.HasAuraExpired("Corruption", 2))
                                )
                            ),

                        new Decorator(
                            ret => !Me.HasAura( "Metamorphosis"),
                            new PrioritySelector(
                                Spell.Cast("Corruption", req => Me.CurrentTarget.HasAuraExpired("Corruption", 3)),
                                Spell.Cast("Hand of Gul'dan", req => Me.CurrentTarget.HasAuraExpired("Shadowflame", 1)),
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

        private static bool NeedToApplyMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCast = false;

            if (!hasAura)
            {
                // aura missing, so check if we should cast to apply 
                shouldCast = Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom") && CurrentDemonicFury >= 60;
                shouldCast = shouldCast || CurrentDemonicFury > 900;
                shouldCast = shouldCast && SpellManager.CanCast("Metamorphosis", Me, false);
            }

            return shouldCast;
        }

        private static bool NeedToCancelMetamorphosis()
        {
            bool hasAura = Me.HasAura("Metamorphosis");
            bool shouldCast = false;

            if (hasAura)
            {
                // aura present, so check if we should cast to remove
                shouldCast = Me.CurrentTarget.HasKnownAuraExpired("Corruption");
                shouldCast = shouldCast && !Me.CurrentTarget.HasAuraExpired("Metamorphosis: Doom", "Doom");
                shouldCast = shouldCast || CurrentDemonicFury < 800;

                // check if we should stay in demon form because of buff (only if we have enough fury to cast)
                shouldCast = shouldCast && !(Me.HasAura("Dark Soul: Knowledge") && CurrentDemonicFury >= 40);
            }

            return shouldCast;
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

        private WoWUnit GetAoeDotTarget( string dotName)
        {
            WoWUnit unit = null;
            if (SpellManager.HasSpell(dotName))
                unit = Common.TargetsInCombat.FirstOrDefault(t => !t.HasMyAura(dotName));

            return unit;
        }

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
                        
                        msg = string.Format(".... h={0:F1}%/m={1:F1}%, fury={2}, metamor={3}, mcore={4}",
                             Me.HealthPercent,
                             Me.ManaPercent,
                             CurrentDemonicFury,
                             Me.HasAura("Metamorphosis"),
                             lstks
                             );

                        if (target != null)
                        {
                            msg += string.Format(", corrupt={0}, doom={1}, enemy={2}%, edist={3:F1}, mobcnt={4}",
                                (long)target.GetAuraTimeLeft("Corruption", true).TotalMilliseconds,
                                (long)target.GetAuraTimeLeft("Doom", true).TotalMilliseconds,
                                (int)target.HealthPercent,
                                target.Distance,
                                0);
                        }

                        Logger.WriteDebug(Color.Wheat, msg);
                        return RunStatus.Failure;
                    })
                )
            );
        }

    }
}