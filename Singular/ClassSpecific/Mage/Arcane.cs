using System;
using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Arcane
    {
        public static TimeSpan EvocateCooldown
        {
            get
            {
                if (!SpellManager.HasSpell("Evocation"))
                    return TimeSpan.MaxValue;

                var left = SpellManager.Spells["Evocation"].CooldownTimeLeft();
                return left;
            }
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Combat)]
        [Context(WoWContext.All)]
        public static Composite CreateArcaneMageCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Helpers.Common.CreateAutoAttack(true),
                //Move away from frozen targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HasAura("Frost Nova") && StyxWoW.Me.CurrentTarget.DistanceSqr < 5 * 5,
                    new Action(
                        ret =>
                            {
                                Logger.Write("Getting away from frozen target");
                                WoWPoint moveTo = WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, StyxWoW.Me.CurrentTarget.Location, 10f);

                                if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                {
                                    Navigator.MoveTo(moveTo);
                                }
                            })),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.BuffSelf("Ice Block", ret => StyxWoW.Me.HealthPercent < 10 && !StyxWoW.Me.ActiveAuras.ContainsKey("Hypothermia")),
                new Decorator(
                    ret => StyxWoW.Me.ActiveAuras.ContainsKey("Ice Block"),
                    new ActionIdle()),
                Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8)),

                Spell.WaitForCast(),
                
                Common.CreateMagePolymorphOnAddBehavior(),

                Spell.Cast("Counterspell", ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast),

                Spell.BuffSelf("Time Warp", ret => StyxWoW.Me.CurrentTarget.HealthPercent > 20 && StyxWoW.Me.CurrentTarget.IsBoss()),

                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.HealthPercent > 50 || StyxWoW.Me.CurrentTarget.IsBoss(),
                    new Sequence(
                        new Action(ctx => StyxWoW.Me.CurrentTarget.Face()),
                        new Action(ctx => StyxWoW.SleepForLagDuration()),
                        Spell.Cast("Flame Orb")
                        )),
                Spell.BuffSelf("Mana Shield", ret => !StyxWoW.Me.Auras.ContainsKey("Mana Shield") && StyxWoW.Me.HealthPercent <= 75),

                // This is our burn rotation. If Evo is coming off CD, make sure we burn as much mana now, as we can. We'll be filling back up soon enough
                new Decorator(ret=>EvocateCooldown.TotalSeconds < 30 && StyxWoW.Me.ManaPercent > 10,
                    new PrioritySelector(
                        // Sigh... CnG's code for mana gems. I shall shoot thee!
                        new Decorator(ret => Common.HaveManaGem() && !Common.ManaGemNotCooldown(),
                            new Action(ctx => Common.UseManaGem())),

                        // AP and Images have 0 range, but aren't melee spells. Ensure the "Target" is ourselves, so the logic knows to automagically ignore range checks.
                        Spell.BuffSelf("Arcane Power"),
                        Spell.BuffSelf("Mirror Image"),
                        Spell.Cast("Flame Orb"),
                        Spell.Cast("Arcane Blast")
                        )),

                // Gets skipped for some odd reason.
                Spell.Cast("Evocation", ret=>StyxWoW.Me, ret => StyxWoW.Me.ManaPercent < 20),

                Spell.Cast("Arcane Missiles", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Arcane Missiles!") && StyxWoW.Me.HasAura("Arcane Blast", 3)),
                Spell.Cast("Arcane Barrage", ret => StyxWoW.Me.HasAura("Arcane Blast", 3)),
                //Spell.BuffSelf("Presence of Mind"),
                Spell.Cast("Arcane Blast"),

                // These 2 are just for support for some DPS until we get arcane blast.
                Spell.Cast("Arcane Barrage", ret=>!SpellManager.HasSpell("Arcane Blast")),
                Spell.Cast("Fireball", ret=>!SpellManager.HasSpell("Arcane Blast")),

                // FFS, don't manually cast slow. So stupid.
                //Spell.Cast("Slow", ret => TalentManager.GetCount(1, 18) < 2 && !StyxWoW.Me.CurrentTarget.ActiveAuras.ContainsKey("Slow") && StyxWoW.Me.CurrentTarget.Distance > 5),
                //Helpers.Common.CreateUseWand(), // Really? Who uses a wand anymore?
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        public static Composite CreateArcaneMagePull()
        {
            return
                new PrioritySelector(
                // Make sure we're in range, and facing the damned target. (LOS check as well)
                    Movement.CreateMoveToLosBehavior(),
                    Movement.CreateFaceTargetBehavior(),
                    Spell.Cast("Arcane Blast"),
                    Movement.CreateMoveToTargetBehavior(true, 35f)
                    );
        }
    }
}
