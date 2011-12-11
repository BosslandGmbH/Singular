using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using TreeSharp;

namespace Singular.ClassSpecific.Warrior
{
    public class Protection
    {
        [Spec(TalentSpec.ProtectionWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        [Context(WoWContext.All)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        public static Composite CreateProtectionWarriorCombat()
        {
            TankManager.NeedTankTargeting = true;
            return new PrioritySelector(
                ctx => TankManager.Instance.FirstUnit ?? StyxWoW.Me.CurrentTarget,

                Spell.BuffSelf("Defensive Stance"),

                //Standard
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Common.CreateAutoAttack(false),

                //Free Heal
                //Spell.Cast("Victory Rush", ret => StyxWoW.Me.CurrentTarget.Distance < 5),
                new Decorator(ret=>StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorEnragedRegenerationHealth,
                    new PrioritySelector(
                        Spell.BuffSelf("Berserker Rage"),
                        Spell.BuffSelf("Enraged Regeneration")
                        )),

                //Defensive Cooldowns
                Spell.BuffSelf("Shield Block"),
                Spell.Cast("Battle Shout", ret => StyxWoW.Me),
                Spell.BuffSelf("Shield Wall", ret => StyxWoW.Me.HealthPercent <= SingularSettings.Instance.Warrior.WarriorProtShieldWallHealth),
                Spell.Buff("Demoralizing Shout", ret => !StyxWoW.Me.CurrentTarget.HasDemoralizing()),

                //Close cap on target
                Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance.Between(8f, TalentManager.HasGlyph("Long Charge") ? 30f : 25f)),
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance.Between(8f, 25f)),
                Spell.CastOnGround(
                    "Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location,
                    ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance <= 40),

                //Interupt or reflect
                Spell.Cast("Spell Reflection", ret => StyxWoW.Me.CurrentTarget.CurrentTarget == StyxWoW.Me && StyxWoW.Me.CurrentTarget.IsCasting),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                //Aoe tanking
                new Decorator(
                    ret => Targeting.GetAggroOnMeWithin(StyxWoW.Me.Location, 15f) > 1,
                    new PrioritySelector(
                        Spell.Buff("Rend"),
                        Spell.Cast("Thunder Clap"),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 2),
                        Spell.Cast("Cleave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 2)
                        )),

                //Taunts
                //If more than 3 taunt, if needs to taunt                
                Spell.Cast(
                    "Challenging Shout", ret => TankManager.Instance.NeedToTaunt.First(),
                    ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.Count(u => u.Distance <= 10) >= 3),
                // If there's a unit that needs taunting, do it.
                Spell.Cast("Taunt", ret => TankManager.Instance.NeedToTaunt.First(), ret => SingularSettings.Instance.EnableTaunting && TankManager.Instance.NeedToTaunt.FirstOrDefault() != null),

                //Single Target
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                //Spell.Cast("Concussion Blow"),
                Spell.Cast("Shield Slam"),
                Spell.Cast("Revenge"),
                Spell.Cast("Heroic Strike", ret => StyxWoW.Me.RagePercent >= 50),
                Spell.Buff("Rend"),
                // Tclap may not be a giant threat increase, but Blood and Thunder will refresh rend. Which all in all, is a good thing.
                // Oh, and the attack speed debuff is win as well.
                Spell.Cast("Thunder Clap"),
                Spell.Cast("Shockwave"),
                Spell.Cast("Devastate"),

                Movement.CreateMoveToTargetBehavior(true, 4f)
                );
        }
    }
}