#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author$
// $Date$
// $HeadURL$
// $LastChangedBy$
// $LastChangedDate$
// $LastChangedRevision$
// $Revision$

#endregion

using System.Linq;

using Singular.Settings;

using Styx.Combat.CombatRoutine;

using TreeSharp;

namespace Singular
{
    partial class SingularRoutine
    {
        #region Combat

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public Composite CreateRetributionPaladinCombat()
        {
            return
                new PrioritySelector(
                    CreateEnsureTarget(),
                    CreateAutoAttack(true),
                    CreateRetributionPaladinCombatBuffs(),
                    //Interrupts
                    new Decorator(
                        ret => Me.CurrentTarget.IsCasting,
                        new PrioritySelector(
                            //Rebuke ID = 96231
                            CreateSpellCast(96231),
                            // Calling Rebuke by name FAILS!!!!!!!!!!! sigh :(
                            //CreateSpellCast("Rebuke", ret => Me.CurrentTarget.IsCasting || Me.CurrentTarget.ChanneledCastingSpellId != null),
                            CreateSpellCast("Hammer of Justice"),
                            CreateSpellCast("Arcane Torrent")
                            )),
                    // Zealotry Routine
                    new Decorator(
                        ret => Me.HasAura("Zealotry"),
                        new PrioritySelector(
                            CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower > 1),
                            CreateSpellCast("Hammer of Wrath"),
                            CreateSpellCast("Exorcism", ret => Me.ActiveAuras.ContainsKey("The Art of War")),
                            CreateSpellCast("Templar's Verdict", ret => Me.CurrentHolyPower > 2 || Me.ActiveAuras.ContainsKey("Hand of Light")),
                            CreateSpellCast("Judgement"),
                            CreateSpellCast("Crusader Strike", ret => Me.CurrentHolyPower < 3)
                            )),
                    // AoE Routine - I know the EJ guide says at 5 but I put it to 3 mainly for dungeons. 
                    new Decorator(
                        ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 3,
                        new PrioritySelector(
                            CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower > 1),
                            CreateSpellCast("Divine Storm", ret => Me.CurrentHolyPower < 3),
                            CreateSpellCast("Hammer of Wrath"),
                            CreateSpellCast("Exorcism", ret => Me.ActiveAuras.ContainsKey("The Art of War")),
                            CreateSpellCast("Templar's Verdict", ret => Me.CurrentHolyPower > 2 || Me.ActiveAuras.ContainsKey("Hand of Light")),
                            CreateSpellCast("Judgement"),
                            CreateSpellCast("Consecration", ret => Me.ManaPercent > 50),
                            CreateSpellCast("Holy Wrath", ret => Me.ManaPercent > 50)
                            )),
                    // Undead Routine
                    new Decorator(
                        ret => CurrentTargetIsUndeadOrDemon,
                        new PrioritySelector(
                            CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower > 1),
                            CreateSpellCast("Crusader Strike", ret => Me.CurrentHolyPower < 3 && NearbyUnfriendlyUnits.Count(u => u.Distance < 8) < 2),
                            CreateSpellCast("Divine Storm", ret => Me.CurrentHolyPower < 3 && NearbyUnfriendlyUnits.Count(u => u.Distance < 8) > 2),
                            CreateSpellCast("Exorcism", ret => Me.ActiveAuras.ContainsKey("The Art of War")),
                            CreateSpellCast("Hammer of Wrath"),
                            CreateSpellCast("Templar's Verdict", ret => Me.CurrentHolyPower > 2 || Me.ActiveAuras.ContainsKey("Hand of Light")),
                            CreateSpellCast("Judgement"),
                            CreateSpellCast("Holy Wrath", ret => Me.ManaPercent > 50),
                            CreateSpellCast("Consecration", ret => Me.ManaPercent > 50)
                            )),
                    // Single Routine - See AoE notes.
                    new Decorator(
                        ret => NearbyUnfriendlyUnits.Count(u => u.Distance < 8) < 3,
                        new PrioritySelector(
                            CreateSpellBuffOnSelf("Inquisition", ret => Me.CurrentHolyPower > 1),
                            CreateSpellCast("Crusader Strike", ret => Me.CurrentHolyPower < 3),
                            CreateSpellCast("Hammer of Wrath"),
                            CreateSpellCast("Exorcism", ret => Me.ActiveAuras.ContainsKey("The Art of War")),
                            CreateSpellCast("Templar's Verdict", ret => Me.CurrentHolyPower > 2 || Me.ActiveAuras.ContainsKey("Hand of Light")),
                            CreateSpellCast("Judgement"),
                            CreateSpellCast("Holy Wrath", ret => Me.ManaPercent > 50),
                            CreateSpellCast("Consecration", ret => Me.ManaPercent > 50)
                            )),
                    //Bot Control
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)


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
        public Composite CreateRetributionPaladinPull()
        {
            return
                new PrioritySelector(
                    CreateSpellCast("Judgement"),
                    // Make sure we're in range, and facing the damned target. (LOS check as well)
                    CreateMoveToAndFace(5f, ret => Me.CurrentTarget)
                    );
        }

        #endregion

        #region Combat Buffs

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateRetributionPaladinCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreateSpellBuffOnSelf("Zealotry", ret => CurrentTargetIsBoss),
                    CreateSpellCast("Word of Glory", ret => Me.CurrentHolyPower >= 2 && Me.HealthPercent <= 75),
                    CreateSpellBuffOnSelf("Avenging Wrath"),
                    CreateSpellBuffOnSelf(
                        "Lay on Hands", ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.LayOnHandsHealthRet && !Me.HasAura("Forbearance")),
                    CreateSpellBuffOnSelf("Divine Protection", ret => Me.HealthPercent <= SingularSettings.Instance.Paladin.DivineProtectionHealthRet)
                    );
        }

        #endregion

        #region Pre-Combat Buffs

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public Composite CreateRetributionPaladinPreCombatBuffs()
        {
            return
                new PrioritySelector(
                    CreatePaladinBuffComposite()
                    );
        }

        #endregion

        /* Priority of Spells - Retribution

                 Single 
         The priority for single-target is as follows: Inq > HoW > Exo > TV > CS > J > HW > Cons
         Against Undead or Demons: Inq > Exo > HoW > TV > CS > J > HW > Cons
         
                 AoE
         The AoE rotation is rather similar to single-target, simply replace Crusader Strike with Divine Storm.
         The rest of the priority stays the same. With Holy Wrath fixed to meteor properly, at 3 targets you
         should begin using Consecration over HW.

         Redcape's modeling shows you should only swap to AoE rotation at 5 or more targets.
        
                 Zealotry
         Zealotry provides a special circumstance. During Zealotry all CS earn 3 HP. 
         Your rotation will become CS, TV, or should HoL proc, CS, TV, TV.
        
         With current values for HoW and Exo, it is currently better to cast a HoW or AoW Exo over a CS or TV even during Zealotry.
         */
    }
}