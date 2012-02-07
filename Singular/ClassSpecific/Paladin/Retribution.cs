using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular.ClassSpecific.Paladin
{
    public class Retribution
    {
        #region Normal Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Normal)]
        public static Composite CreateRetributionPaladinNormalPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                    new PrioritySelector(
                        // Cooldowns
                        Spell.BuffSelf("Zealotry"),
                        Spell.BuffSelf("Avenging Wrath"),
                        Spell.BuffSelf("Guardian of Ancient Kings"),
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation
                Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower == 3),
                Spell.Cast("Hammer of Justice", ret => StyxWoW.Me.HealthPercent <= 40),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Templar's Verdict",
                    ret => StyxWoW.Me.CurrentHolyPower == 3 &&
                           (StyxWoW.Me.HasAura("Inquisition") || !SpellManager.HasSpell("Inquisition"))),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement"),
                Spell.BuffSelf("Holy Wrath"),
                Spell.BuffSelf("Consecration"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Battleground Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Battlegrounds)]
        public static Composite CreateRetributionPaladinPvPPullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                           StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),
                Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                // Cooldowns
                Spell.BuffSelf("Zealotry"),
                Spell.BuffSelf("Avenging Wrath"),
                Spell.BuffSelf("Guardian of Ancient Kings"),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation
                Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower == 3),
                Spell.Cast("Hammer of Justice", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 40),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Templar's Verdict",
                    ret => StyxWoW.Me.CurrentHolyPower == 3 &&
                           (StyxWoW.Me.HasAura("Inquisition") || !SpellManager.HasSpell("Inquisition"))),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement"),
                Spell.BuffSelf("Holy Wrath"),
                Spell.BuffSelf("Consecration"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Instance Rotation

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Pull)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.Instances)]
        public static Composite CreateRetributionPaladinInstancePullAndCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Movement.CreateMoveBehindTargetBehavior(),

                // Defensive
                Spell.BuffSelf("Hand of Freedom",
                    ret => !StyxWoW.Me.Auras.Values.Any(a => a.Name.Contains("Hand of") && a.CreatorGuid == StyxWoW.Me.Guid) &&
                           StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                          WoWSpellMechanic.Disoriented,
                                                          WoWSpellMechanic.Frozen,
                                                          WoWSpellMechanic.Incapacitated,
                                                          WoWSpellMechanic.Rooted,
                                                          WoWSpellMechanic.Slowed,
                                                          WoWSpellMechanic.Snared)),
                Spell.BuffSelf("Divine Shield", ret => StyxWoW.Me.HealthPercent <= 20 && !StyxWoW.Me.HasAura("Forbearance")),

                // Cooldowns
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                    Spell.BuffSelf("Zealotry"),
                    Spell.BuffSelf("Avenging Wrath"),
                    Spell.BuffSelf("Guardian of Ancient Kings"))),

                // AoE Rotation
                new Decorator(
                    ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Divine Storm"),
                        Spell.BuffSelf("Consecration"),
                        Spell.BuffSelf("Holy Wrath")
                        )),

                // Rotation
                Spell.BuffSelf("Inquisition", ret => StyxWoW.Me.CurrentHolyPower == 3),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Templar's Verdict", 
                    ret => StyxWoW.Me.CurrentHolyPower == 3 && 
                           (StyxWoW.Me.HasAura("Inquisition") || !SpellManager.HasSpell("Inquisition"))),
                Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),
                Spell.Cast("Judgement"),
                Spell.BuffSelf("Holy Wrath"),
                Spell.BuffSelf("Consecration"),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion




        #region Combat

        [Class(WoWClass.Paladin)]
        [Spec(TalentSpec.RetributionPaladin)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Heal)]
        [Context(WoWContext.All)]
        public static Composite CreateRetributionPaladinCombat()
        {
            /*
http://us.battle.net/wow/en/forum/topic/2267589223?page=1#2

What you need to realize about Retribution's rotation is that it's actually a loose priority system. Hitting your buttons in the suboptimal order will not put your meters in the dirt, but you will of course be performing at a suboptimal standard (go figure).

3.1 Single Target

Note that opening rotations may differ from what you read in this section due to initial CD usage. Optimal opening rotations are discussed in section 4 (Cooldowns).

Inquisition > Crusader Strike > Hammer of Wrath > Exorcism > Templar's Verdict > Judgement > Holy Wrath > Consecration
             */
            return
                new PrioritySelector(
                    Safers.EnsureTarget(),
                    new Decorator(
                        ret => Group.Healers.Count == 0,
                        Holy.CreatePaladinHealBehavior(true)),
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Helpers.Common.CreateAutoAttack(true),

                    // Interrupt the first unit casting near us. Huzzah!
                    Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                    Movement.CreateMoveBehindTargetBehavior(),
                // The below is the old combat rotation. I'm rewriting it from scratch to maximize DPS according to Noxxic.com, EJ.com and my own personal findings.
                // Along with the findings of other high-end ret paladins.

                    // Single Target
                    Spell.Cast("Inquisition"),
                // Pop Crusader if we're in single-target mode.
                    Spell.Cast("Crusader Strike", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 3 || !SpellManager.HasSpell("Divine Storm")),
                // Pop Divine Storm if we're trying to AoE.
                    Spell.Cast("Divine Storm", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 3),

                    // This gets moved up to 3rd if the target is undead/demon. Otherwise, its lower in the prio. (Does this really add *that* much of a DPS boost that we need it?)
                // ofc a mana free dps fucking does.
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War")),

                    //Spell.Cast(
                    //    "Exorcism",
                    //    ret =>
                    //    StyxWoW.Me.CurrentTarget.IsUndeadOrDemon() &&
                    //    StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War") &&
                    //    Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 3),

                    Spell.Cast("Hammer of Wrath", ret => StyxWoW.Me.CurrentTarget.HealthPercent <= 20),
                    Spell.Cast("Templar's Verdict", ret => StyxWoW.Me.CurrentHolyPower == 3),
                // Don't use Exorcism if we're AOEing. 
                    Spell.Cast("Exorcism", ret => StyxWoW.Me.ActiveAuras.ContainsKey("The Art of War") && Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) < 3),
                    Spell.Cast("Judgement"),
                // These 2 are "flipped" for AOE and Single-target. Now, personally, since Cons is a 30s CD, and HW is a 15s CD, there's really no DPS gain by flipping them.
                // So just leave them as-is and don't bother messing with it!
                    Spell.Cast("Holy Wrath"),
                    Spell.Cast("Consecration", ret => StyxWoW.Me.CurrentTarget.IsBoss() || Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 3),


                    // Move to melee is LAST. Period.
                    Movement.CreateMoveToMeleeBehavior(true)
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
                    Movement.CreateMoveToTargetBehavior(true, 5f)
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
                    Spell.BuffSelf("Zealotry", ret => !StyxWoW.Me.IsInInstance || StyxWoW.Me.CurrentTarget.IsBoss()),
                    Spell.BuffSelf("Avenging Wrath"),
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
