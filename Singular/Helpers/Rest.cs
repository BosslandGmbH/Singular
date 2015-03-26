using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using System;

using Action = Styx.TreeSharp.Action;
using System.Drawing;
using Singular.Managers;
using Singular.Utilities;
using Styx.CommonBot.Routines;

namespace Singular.Helpers
{
    internal static class Rest
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        private static bool CorpseAround
        {
            get
            {
                return ObjectManager.GetObjectsOfType<WoWUnit>(true, false).Any(
                    u => u.Distance < 5 && u.IsDead &&
                         (u.CreatureType == WoWCreatureType.Humanoid || u.CreatureType == WoWCreatureType.Undead));
            }
        }

        private static bool PetInCombat
        {
            get { return Me.GotAlivePet && Me.PetInCombat; }
        }

        /// <summary>
        /// implements standard Rest behavior.  self-heal optional and typically used by DPS that have healing spell, 
        /// as healing specs are better served using a spell appropriate to amount of healing needed.  ressurrect
        /// is optional and done only if spell name passed
        /// </summary>
        /// <param name="spellHeal">name of healing spell</param>
        /// <param name="spellRez">name of ressurrect spell</param>
        /// <returns></returns>
        public static Composite CreateDefaultRestBehaviour(string spellHeal = null, string spellRez = null)
        {
            return new PrioritySelector(

                new Decorator(
                    ret => !Me.IsDead && !Me.IsGhost,
                    new PrioritySelector(

                // Self-heal if possible
                        new Decorator(
                            ret =>
                            {
                                if (Me.HasAnyAura("Drink", "Food", "Refreshment"))
                                    return false;
                                if (spellHeal == null || Me.HealthPercent > 85)
                                    return false;
                                if (!SpellManager.HasSpell(spellHeal))
                                    return false;
                                if (!Spell.CanCastHack(spellHeal, Me))
                                {
                                    Logger.WriteDebug("DefaultRest: CanCast failed for {0}", spellHeal);
                                    return false;
                                }
                                if (Me.PredictedHealthPercent(includeMyHeals: true) > 85)
                                    return false;
                                return true;
                            },
                            new Sequence(
                                new PrioritySelector(
                                    Movement.CreateEnsureMovementStoppedBehavior(reason: "to heal"),
                                    new Wait(TimeSpan.FromMilliseconds(500), until => !Me.IsMoving, new ActionAlwaysSucceed())
                                    ),
                                new PrioritySelector(
                                    new Sequence(
                                        ctx => Me.HealthPercent,
                                        new Action(r => Logger.WriteDebug("Rest Heal - {0} @ {1:F1}% Predict:{2:F1}% and moving:{3}, cancast:{4}",
                                            spellHeal, (double)r, Me.PredictedHealthPercent(includeMyHeals: true), Me.IsMoving, Spell.CanCastHack(spellHeal, Me, skipWowCheck: false))),
                                        Spell.Cast(spellHeal,
                                            mov => true,
                                            on => Me,
                                            req => true,
                                            cancel =>
                                            {
                                                if (Me.HealthPercent < 90)
                                                    return false;
                                                Logger.WriteDiagnostic("Rest Heal - cancelling since health reached {0:F1}%", Me.HealthPercent);
                                                return true;
                                            },
                                            LagTolerance.No
                                            ),
                                        new Action(r => Logger.WriteDebug("Rest - Before Heal Cast Wait")),
                                        new WaitContinue(TimeSpan.FromMilliseconds(1500), until => Spell.CanCastHack(spellHeal, Me), new ActionAlwaysSucceed()),
                                        new Action(r => Logger.WriteDebug("Rest - Before Heal Health Increase Wait")),
                                        new WaitContinue(TimeSpan.FromMilliseconds(500), until => Me.HealthPercent > (1.1 * ((double)until)), new ActionAlwaysSucceed()),
                                        new Action(r => Logger.WriteDebug("Rest - Before Heal Cast Wait")),
                                        new Action(r =>
                                        {
                                            Logger.WriteDebug("Rest - After Heal Completed: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true));
                                            return RunStatus.Success;
                                        })
                                        ),

                                    new Action(r => {
                                        Logger.WriteDebug("Rest - After Heal Skipped: {0:F1}% Predicted: {1:F1}%", Me.HealthPercent, Me.PredictedHealthPercent(includeMyHeals: true));
                                        return RunStatus.Failure;
                                        })
                                    )
                                )
                            ),

                // Make sure we wait out res sickness. 
                        Helpers.Common.CreateWaitForRessSickness(),

                // Cannibalize support goes before drinking/eating. changed to a Sequence with wait because Rest behaviors that had a 
                // .. WaitForCast() before call to DefaultRest would prevent cancelling when health/mana reached
                        new Decorator(
                            ret => SingularSettings.Instance.UseRacials
                                && (Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth || (Me.PowerType == WoWPowerType.Mana && Me.ManaPercent <= SingularSettings.Instance.MinMana))
                                && Spell.CanCastHack("Cannibalize")
                                && CorpseAround,
                            new Sequence(
                                new DecoratorContinue(ret => Me.IsMoving, Movement.CreateEnsureMovementStoppedBehavior(reason: "to cannibalize")),
                                new Wait(1, ret => !Me.IsMoving, new ActionAlwaysSucceed()),
                                new Action(ret => Logger.Write(LogColor.SpellHeal, "*Cannibalize @ health:{0:F1}%{1}", Me.HealthPercent, (Me.PowerType != WoWPowerType.Mana) ? "" : string.Format(" mana:{0:F1}%", Me.ManaPercent))),
                                new Action(ret => Spell.CastPrimative("Cannibalize")),

                                // wait until Cannibalize in progress
                                new WaitContinue(
                                    1,
                                    ret => Me.CastingSpell != null && Me.CastingSpell.Name == "Cannibalize",
                                    new ActionAlwaysSucceed()
                                    ),
                // wait until cast or healing complete. use actual health percent here
                                new WaitContinue(
                                    10,
                                    ret => Me.CastingSpell == null
                                        || Me.CastingSpell.Name != "Cannibalize"
                                        || (Me.HealthPercent > 95 && (Me.PowerType != WoWPowerType.Mana || Me.ManaPercent > 95)),
                                    new ActionAlwaysSucceed()
                                    ),
                // show completion message and cancel cast if needed
                                new Action(ret =>
                                {
                                    bool stillCasting = Me.CastingSpell != null && Me.CastingSpell.Name == "Cannibalize";
                                    Logger.WriteFile("{0} @ health:{1:F1}%{2}",
                                        stillCasting ? "/cancel Cannibalize" : "Cannibalize ended",
                                        Me.HealthPercent,
                                        (Me.PowerType != WoWPowerType.Mana) ? "" : string.Format(" mana:{0:F1}%", Me.ManaPercent)
                                        );

                                    if (stillCasting)
                                    {
                                        SpellManager.StopCasting();
                                    }
                                })
                                )
                            ),

                // use a bandage if enabled (it's quicker)
                        new Decorator(
                            ret => Me.IsAlive && Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth,
                            Item.CreateUseBandageBehavior()
                            ),

                // Make sure we're a class with mana, if not, just ignore drinking all together! Other than that... same for food.
                        new Decorator(
                            ret => !Me.Combat
                                && !Me.Stunned
                                && !Me.IsSwimming && (Me.PowerType == WoWPowerType.Mana || Me.Class == WoWClass.Druid)
                                && Me.ManaPercent <= SingularSettings.Instance.MinMana
                                && !Me.HasAnyAura("Drink", "Refreshment") && Consumable.GetBestDrink(false) != null,
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(reason: "to drink"),
                                new Sequence(
                                    new Action(ret =>
                                    {
                                        Logger.Write("Drinking @ {0:F1}% mana", Me.ManaPercent);
                                        Styx.CommonBot.Rest.DrinkImmediate();
                                    }),
                                    Helpers.Common.CreateWaitForLagDuration(),
                                    new PrioritySelector(
                                        new Wait(TimeSpan.FromMilliseconds(500), until => Me.HasAnyAura("Drink", "Refreshment"), new ActionAlwaysSucceed()),
                                        new Action(r => Logger.WriteDiagnostic("Drinking: failed to see 'Drink' aura"))
                                        )
                                    )
                                )
                            ),

                // Check if we're allowed to eat (and make sure we have some food. Don't bother going further if we have none.
                        new Decorator(
                            ret => !Me.Combat 
                                && !Me.Stunned
                                && !Me.IsSwimming
                                && Me.PredictedHealthPercent(includeMyHeals: true) <= SingularSettings.Instance.MinHealth
                                && !Me.HasAnyAura("Food", "Refreshment") && Consumable.GetBestFood(false) != null,
                            new PrioritySelector(
                                Movement.CreateEnsureMovementStoppedBehavior(reason: "to eat"),
                                new Sequence(
                                    new Action(
                                        ret =>
                                        {
                                            float myHealth = Me.PredictedHealthPercent(includeMyHeals: true);
                                            Logger.WriteDebug("NeedToEat:  predictedhealth @ {0:F1}", myHealth);
                                            Logger.Write("Eating @ {0:F1}% health", Me.HealthPercent);
                                            Styx.CommonBot.Rest.FeedImmediate();
                                        }),
                                    Helpers.Common.CreateWaitForLagDuration(),
                                    new PrioritySelector(
                                        new Wait(TimeSpan.FromMilliseconds(500), until => Me.HasAnyAura("Food", "Refreshment"), new ActionAlwaysSucceed()),
                                        new Action( r => Logger.WriteDiagnostic("Eating: failed to see 'Food' aura"))
                                        )
                                    )
                                )
                            ),

                        // STAY SEATED (in Rest) while eating/drinking aura active.
                //  true: stay seated
                //  false:  allow interruption of Drink
                        new Decorator(
                            ret => Me.HasAnyAura("Drink", "Refreshment")
                                && (Me.PowerType == WoWPowerType.Mana || Me.Specialization == WoWSpec.DruidFeral)
                                && Me.ManaPercent < 95,
                            new ActionAlwaysSucceed()
                            ),

                        new Decorator(
                            req => Me.HasAnyAura("Food", "Refreshment") && Me.HealthPercent < 95,
                            new PrioritySelector(
                                new Decorator(
                                    req => spellHeal != null
                                        && Spell.CanCastHack(spellHeal, Me)
                                        && Me.PowerType == WoWPowerType.Mana
                                        && Me.ManaPercent >= 70
                                        && Me.HealthPercent < 85,
                                    new Sequence(
                                        new Action(r => Logger.Write(LogColor.Hilite, "^Stop eating and '{0}' since mana reached {1:F1}%", spellHeal, Me.ManaPercent)),
                                        new Action(r => Me.CancelAura("Food")),
                                        new Wait(TimeSpan.FromMilliseconds(500), until => !Me.HasAura("Food"), new ActionAlwaysSucceed())
                                        )
                                    ),

                                new ActionAlwaysSucceed()   // keep eating then
                                )
                            ),

                // wait here if we are moving -OR- do not have food or drink
                        new Decorator(
                            ret => WaitForRegenIfNoFoodDrink(),

                            new PrioritySelector(
                                new Decorator(
                                    ret => Me.IsMoving,
                                    new PrioritySelector(
                                        new Throttle(5, new Action(ret => Logger.Write("Still moving... waiting until Bot stops"))),
                                        new ActionAlwaysSucceed()
                                        )
                                    ),
                                new Decorator(
                                    req => Me.IsSwimming,
                                    new PrioritySelector(
                                        new Throttle(15, new Action(ret => Logger.Write("Swimming. Waiting to recover our health/mana back"))),
                                        new ActionAlwaysSucceed()
                                        )
                                    ),
                                new PrioritySelector(
                                    new Throttle(15, new Action(ret => Logger.Write("We have no food/drink. Waiting to recover our health/mana back"))),
                                    new ActionAlwaysSucceed()
                                    )
                                )
                            ),

                // rez anyone near us if appropriate
                        new Decorator(ret => spellRez != null, Spell.Resurrect(spellRez)),

                // hack:  some bots not calling PreCombatBuffBehavior, so force the call if we are about done resting
                // SingularRoutine.Instance.PreCombatBuffBehavior,

                        Movement.CreateWorgenDarkFlightBehavior()
                        )
                    )
                );
        }

        /// <summary>
        /// checks if we should stay in current spot and wait for health and/or mana to regen.
        /// called when we have no food/drink
        /// </summary>
        /// <returns></returns>
        private static bool WaitForRegenIfNoFoodDrink()
        {
            // never wait in a battleground
            if (Me.CurrentMap.IsBattleground)
                return false;

            // don't wait if we are combat bugged (or could be mob in combat with pet at distance)
            if (Me.Combat)
                return false;

            // always wait for health to regen
            if (Me.HealthPercent < SingularSettings.Instance.MinHealth)
                return true;
            
            // non-mana users don't wait mana
            if (Me.PowerType != WoWPowerType.Mana)
                return false;

            // ferals and guardians dont wait on mana either
            if (TalentManager.CurrentSpec == WoWSpec.DruidFeral || TalentManager.CurrentSpec == WoWSpec.DruidGuardian )
                return false;
                
            // wait for mana if too low
            if (Me.ManaPercent < SingularSettings.Instance.MinMana)
                return true;

            return false;
        }
    }
}
