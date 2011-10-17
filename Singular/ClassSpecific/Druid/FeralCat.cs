using System.Linq;

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Lists;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;

using TreeSharp;

namespace Singular.ClassSpecific.Druid
{
    public class FeralCat
    {

        [Spec(TalentSpec.FeralDruid)]
        //[Spec(TalentSpec.FeralTankDruid)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Druid)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFeralCatCombat()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.Shapeshift != ShapeshiftForm.Cat,
                    Spell.BuffSelf("Cat Form")),

                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                
                new Decorator(ret=>!StyxWoW.Me.IsInRaid && !StyxWoW.Me.IsInParty,
                    Resto.CreateRestoDruidHealOnlyBehavior(true)),

                // Drop aggro if we're in trouble.
                new Decorator(
                    ret => (StyxWoW.Me.IsInRaid || StyxWoW.Me.IsInParty) && StyxWoW.Me.CurrentTarget.ThreatInfo.RawPercent > 50,
                    Spell.Cast("Cower")),

                //Spell.Cast("Faerie Fire (Feral)", ret=>!Unit.HasAura(StyxWoW.Me.CurrentTarget, "Faerie Fire", 3)),
                //new Action(ret=>Logger.WriteDebug("Done with FF Going into boss check")),
                // 
                Spell.Cast("Faerie Fire (Feral)", ret => !StyxWoW.Me.CurrentTarget.HasSunders() && !StyxWoW.Me.CurrentTarget.HasAura("Faerie Fire", 3)),

                // ... interrupts :)
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                // Keep the debuff up at all times. We'll be spamming Mangle further down the prio list if we can't use shred
                Spell.Cast("Mangle (Cat)", ret => !StyxWoW.Me.CurrentTarget.HasAnyAura("Mangle", "Hemorrhage", "Trauma")),

                // On cooldown, only when we actually can use the full energy it gives.
                Spell.BuffSelf("Tiger's Fury", ret => StyxWoW.Me.CurrentEnergy < 40),
                // Again... on cooldown
                Spell.BuffSelf("Berserk"),

                // Deal with AOE here. We wanna be popping TF and Zerk for swipe spam. Really only useful when there's more than 5 of them to swipe.
                Spell.Cast("Swipe", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 5),

                // We shred if omen of clarity procs. We want this high in the prio since its an expensive attack. We want to make full use of it!
                Spell.Cast("Shred", ret => StyxWoW.Me.HasAura("Omen of Clarity") && StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget)),

                // Keep this up at all times with 5cp finishers. Refresh it with 3s left to ensure it stays up.
                Spell.Cast("Rip", ret => StyxWoW.Me.ComboPoints == 5 && StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 3),

                // And the same with Rake
                Spell.Cast("Rake", ret => StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3),

                // Gotta keep this up. But not at the expense of damage abilities.
                Spell.BuffSelf("Savage Roar"),

                // Most pages put this below normal shred generators. But its here to ensure it actually gets used.
                // If we put it below shred, then we'll never actually pop FB
                Spell.Cast("Ferocious Bite", ret => StyxWoW.Me.ComboPoints == 5 && (StyxWoW.Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 5||!SpellManager.HasSpell("Rip"))),

                // This is our main CP generator. Use it at all times.
                Spell.Cast("Shred", ret => StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget)),

                // And the fallback...
                Spell.Cast("Mangle (Cat)", ret => !StyxWoW.Me.IsBehind(StyxWoW.Me.CurrentTarget) || !SpellManager.HasSpell("Shred")),



                // Since we can't get 'behind' mobs, just do this, kaythanks
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }
    }
}
