using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using CommonBehaviors.Actions;
using System;
using Singular.Utilities;

namespace Singular.ClassSpecific.Paladin
{
    public class Retribution
    {

        #region Properties & Fields

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }

        private const int RET_T13_ITEM_SET_ID = 1064;

        private static int NumTier13Pieces
        {
            get
            {
                return StyxWoW.Me.CarriedItems.Count(i => i.ItemInfo.ItemSetId == RET_T13_ITEM_SET_ID);
            }
        }

        private static bool Has2PieceTier13Bonus { get { return NumTier13Pieces >= 2; } }

        private static int _mobCount;

        #endregion

        #region Rest

        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinRetribution)]
        public static Composite CreatePaladinRetributionRest()
        {
            return new PrioritySelector(
                Spell.WaitForCastOrChannel(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Rest.CreateDefaultRestBehaviour( "Flash of Light", "Redemption")
                        )
                    )
                );
        }

        #endregion

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.All)]
        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinRetribution, WoWContext.All)]
        public static Composite CreatePaladinRetributionNormalPullAndCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),

                Spell.WaitForCastOrChannel(),

                new Decorator( 
                    ret => !Spell.IsGlobalCooldown() && Me.GotTarget(),
                    new PrioritySelector(

                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        // aoe count
                        ActionAoeCount(),

                        CreateRetDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreatePaladinPullMore(),

                        // Defensive
                        Spell.BuffSelf("Hand of Freedom",
                            ret => Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                                  WoWSpellMechanic.Disoriented,
                                                                  WoWSpellMechanic.Frozen,
                                                                  WoWSpellMechanic.Incapacitated,
                                                                  WoWSpellMechanic.Rooted,
                                                                  WoWSpellMechanic.Slowed,
                                                                  WoWSpellMechanic.Snared)),

                        Common.CreatePaladinSealBehavior(),

                        Spell.Cast( "Hammer of Justice", ret => PaladinSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal ),
                        Spell.Cast( 
                            "War Stomp", 
                            req => PaladinSettings.StunMobsWhileSolo 
                                && Me.Race == WoWRace.Tauren
                                && EventHandlers.TimeSinceAttackedByEnemyPlayer.TotalSeconds < 5
                                && EventHandlers.AttackingEnemyPlayer != null 
                                && EventHandlers.AttackingEnemyPlayer.SpellDistance() < 8
                            ),

                        //7	Blow buffs seperatly.  No reason for stacking while grinding.
                        Spell.BuffSelf(
                            "Holy Avenger", 
                            req => PaladinSettings.RetAvengAndGoatK
                                && Me.GotTarget()
                                && Me.CurrentTarget.IsWithinMeleeRange && !Me.CurrentTarget.IsTrivial()
                                && (_mobCount > 1 || Me.CurrentTarget.TimeToDeath() > 25)
                                && (!Me.HasAura("Avenging Wrath") && Spell.GetSpellCooldown("Avenging Wrath").TotalSeconds > 1)
                            ),

                        Spell.BuffSelf(
                            "Avenging Wrath", 
                            req => PaladinSettings.RetAvengAndGoatK
                                && Me.GotTarget()
                                && Me.CurrentTarget.IsWithinMeleeRange && !Me.CurrentTarget.IsTrivial()
                                && (_mobCount > 1 || Me.CurrentTarget.TimeToDeath() > 25)
                                && (!Me.HasAura("Holy Avenger") && Spell.GetSpellCooldown("Holy Avenger").TotalSeconds > 1)
                            ),

                        Spell.Cast("Execution Sentence", ret => Me.CurrentTarget.TimeToDeath() > 15),
                        Spell.Cast("Holy Prism", on => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Distance < 40)),

                        // lowbie farming priority
                        new Decorator(
                            ret => _mobCount > 1 && Spell.UseAOE && Me.CurrentTarget.IsTrivial(),
                            new PrioritySelector(
                                // Bobby53: Inq > 5HP DS > Exo > HotR > 3-4HP DS
                                Spell.Cast("Divine Storm", ret => Me.CurrentHolyPower == 5 && Spell.UseAOE),
                                Spell.Cast("Exorcism", req => TalentManager.HasGlyph("Mass Exorcism")),
                                Spell.Cast("Hammer of the Righteous"),
                                Spell.Cast("Divine Storm", ret => Me.CurrentHolyPower >= 3 && Spell.UseAOE)
                                )
                            ),

                        Common.CreatePaladinBlindingLightBehavior(),

                        new Decorator(
                            ret => _mobCount >= 2 && Spell.UseAOE,
                            new PrioritySelector(

                                // was EJ: Inq > 5HP DS > LH > HoW > Exo > HotR > Judge > 3-4HP DS (> SS)
                                // now EJ: Inq > 5HP DS > LH > HoW (> T16 Free DS) > HotR > Judge > Exo > 3-4HP DS (> SS)
                                Spell.Cast(Spell.UseAOE && SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower == 5),
                                Spell.CastOnGround("Light's Hammer", on => Me.CurrentTarget, ret => 2 <= Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f)),
                                Spell.Cast("Divine Storm", req => Spell.UseAOE && Me.HasAura("Divine Crusader")),   // T16 buff
                                Spell.Cast(SpellManager.HasSpell("Hammer of the Righteous") ? "Hammer of the Righteous" : "Crusader Strike"),
                                Spell.Cast("Judgment"),
                                Spell.Cast("Exorcism"),
                                Spell.Cast(Spell.UseAOE && SpellManager.HasSpell("Divine Storm") ? "Divine Storm" : "Templar's Verdict", ret => Me.CurrentHolyPower >= 3),
                                Spell.BuffSelf("Sacred Shield"),
                                Movement.CreateMoveToMeleeBehavior(true),
                                new ActionAlwaysSucceed()
                                )
                            ),

                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Blade of Wrath"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Execution Sentence", when => Me.CurrentTarget.TimeToDeath() > 8),
                        Spell.Cast("Justicar's Vengeance", when => Me.HasAura("Divine Purpose")),
                        Spell.Cast("Templar's Verdict", when => Me.CurrentTarget.HasAura("Judgment"))
                        )
                    ),

                // Move to melee is LAST. Period.
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        private static Action ActionAoeCount()
        {
            return new Action(ret =>
            {
                _mobCount = Unit.NearbyUnfriendlyUnits.Count(u => u.SpellDistance() < 8);
                return RunStatus.Failure;
            });
        }

        #endregion

        private static Composite CreateRetDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new Action( ret => { return RunStatus.Failure; } );

            return new Sequence(
                new ThrottlePasses(
                    1, 
                    TimeSpan.FromMilliseconds(1500), 
                    RunStatus.Failure,
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, m={1:F1}%, moving={2}, mobs={3}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving.ToYN(),
                            _mobCount
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, face={3}, loss={4}, stun={5}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                Me.IsSafelyFacing(target).ToYN(),
                                target.InLineOfSpellSight.ToYN(),
                                target.IsStunned().ToYN()
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }

    }

    public class TMsg : Decorator
    {
        public static bool ShowTraceMessages { get; set; }

        public TMsg(SimpleStringDelegate str)
            : base(ret => ShowTraceMessages, new Action(r => { Logger.WriteDebug(str(r)); return RunStatus.Failure; }))
        {
        }
    }

}
