using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Retribution
    {
        #region Combat

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinCombat()
        {
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),
                //Interrupts
                    new Decorator(
                        ret => StyxWoW.Me.CurrentTarget.IsCasting,
                        new PrioritySelector(
                //Rebuke ID = 96231
                            Spell.Cast(96231),
                // Calling Rebuke by name FAILS!!!!!!!!!!! sigh :(
                //Spell.Cast("Rebuke", ret => StyxWoW.Me.CurrentTarget.IsCasting || StyxWoW.Me.CurrentTarget.ChanneledCastingSpellId != null),
                            Spell.Cast("Hammer of Justice"),
                            Spell.Cast("Arcane Torrent")
                            )),
                // Zealotry Routine
                    new Decorator(
                        ret => StyxWoW.Me.HasAura("Zealotry"),
                        new PrioritySelector(
                            Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower > 1),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                            Spell.Cast("Templar's Verdict", ret => StyxWoW.Me.CurrentHolyPower > 2 || StyxWoW.Me.ActiveAuras.ContainsKey("Hand of Light")),
                            Spell.Cast("Judgement"),
                            Spell.Cast("Crusader Strike", ret => StyxWoW.Me.CurrentHolyPower < 3)
                            )),
                // AoE Routine - I know the EJ guide says at 5 but I put it to 3 mainly for dungeons. 
                    new Decorator(
                        ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) >= 3,
                        new PrioritySelector(
                            Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower > 1),
                            Spell.Cast("Divine Storm", ret => StyxWoW.Me.CurrentHolyPower < 3),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                            Spell.Cast("Templar's Verdict", ret => StyxWoW.Me.CurrentHolyPower > 2 || StyxWoW.Me.ActiveAuras.ContainsKey("Hand of Light")),
                            Spell.Cast("Judgement"),
                            Spell.Cast("Consecration", ret => StyxWoW.Me.ManaPercent > 50),
                            Spell.Cast("Holy Wrath", ret => StyxWoW.Me.ManaPercent > 50)
                            )),
                // Undead Routine
                    new Decorator(
                        ret => Unit.IsUndeadOrDemon(StyxWoW.Me.CurrentTarget),
                        new PrioritySelector(
                            Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower > 1),
                            Spell.Cast("Crusader Strike", ret => StyxWoW.Me.CurrentHolyPower < 3 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) < 2),
                            Spell.Cast("Divine Storm", ret => StyxWoW.Me.CurrentHolyPower < 3 && Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) > 2),
                            Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Templar's Verdict", ret => StyxWoW.Me.CurrentHolyPower > 2 || StyxWoW.Me.ActiveAuras.ContainsKey("Hand of Light")),
                            Spell.Cast("Judgement"),
                            Spell.Cast("Holy Wrath", ret => StyxWoW.Me.ManaPercent > 50),
                            Spell.Cast("Consecration", ret => StyxWoW.Me.ManaPercent > 50)
                            )),
                // Single Routine - See AoE notes.
                    new Decorator(
                        ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 8*8) < 3,
                        new PrioritySelector(
                            Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower > 1),
                            Spell.Cast("Crusader Strike", ret => StyxWoW.Me.CurrentHolyPower < 3),
                            Spell.Cast("Hammer of Wrath"),
                            Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                            Spell.Cast("Templar's Verdict", ret => StyxWoW.Me.CurrentHolyPower > 2 || StyxWoW.Me.ActiveAuras.ContainsKey("Hand of Light")),
                            Spell.Cast("Judgement"),
                            Spell.Cast("Holy Wrath", ret => StyxWoW.Me.ManaPercent > 50),
                            Spell.Cast("Consecration", ret => StyxWoW.Me.ManaPercent > 50)
                            )),
                //Bot Control
                    Movement.CreateMoveToTargetBehavior(true,5f)


                    // to check it was routine prioritizing correctly or just a general logger due to all the green spell spam that is by default :(
                //new Action(delegate
                //        {
                //            Logger.Write("-- END -- ");
                //        }
                //        )
                    );
        }

        #endregion

        #region Pull

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinPull()
        {
            return
                new PrioritySelector(
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Judgement"),
                    Helpers.Common.CreateAutoAttack(true),
                    Movement.CreateMoveToTargetBehavior(true,5f)
                    );
        }

        #endregion

        #region Combat Buffs

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    Spell.BuffSelf("Zealotry", ret => StyxWoW.Me.CurrentTarget.IsBoss()),
                    Spell.Cast("Word of Glory", ret => StyxWoW.Me.CurrentHolyPower >= 2 && StyxWoW.Me.HealthPercent <= 75),
                    Spell.BuffSelf("Avenging Wrath"),
                    Spell.BuffSelf(
                        "Lay on Hands", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthRet && !StyxWoW.Me.HasAura("Forbearance")),
                    Spell.BuffSelf("Divine Protection", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthRet)
                    );
        }

        #endregion

        /* Priority of Spells - Retribution

                 Single 
         The priority for single-target is as follows: Inq > HoW > Exo > TV > CS > J > HW > Cons
         Against Undead or Demons: Inq > Exo > HoW > TV > CS > J > HW > Cons
         
                 AoE
         The AoE rotation is rather similar to single-target, simply replace Crusader Strike with Divine Storm.
         The rest of the priority stays the saStyxWoW.Me. With Holy Wrath fixed to meteor properly, at 3 targets you
         should begin using Consecration over HW.

         Redcape's modeling shows you should only swap to AoE rotation at 5 or more targets.
        
                 Zealotry
         Zealotry provides a special circumstance. During Zealotry all CS earn 3 HP. 
         Your rotation will become CS, TV, or should HoL proc, CS, TV, TV.
        
         With current values for HoW and Exo, it is currently better to cast a HoW or AoW Exo over a CS or TV even during Zealotry.
         */
    }
}
