using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;
using Styx;
using System.Linq;

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {
        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Context(WoWContext.All)]
        [Behavior(BehaviorType.Combat)]
        [Behavior(BehaviorType.Pull)]
        public static Composite CreateDemonologyCombat()
        {
            PetManager.WantedPet = "Felguard";
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                new Decorator(
                    ret => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Hellfire" && StyxWoW.Me.HealthPercent < 70,
                    new Action(ret => SpellManager.StopCasting())),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),
                Spell.BuffSelf("Soulburn", ret => SpellManager.HasSpell("Soul Fire") || StyxWoW.Me.HealthPercent < 70),
                Spell.Cast("Life Tap", ret => StyxWoW.Me.ManaPercent < 50 && StyxWoW.Me.HealthPercent > 70),
                new Decorator(ret => StyxWoW.Me.CurrentTarget.Fleeing,
                    Pet.CreateCastPetAction("Axe Toss")),
                new Decorator(ret => StyxWoW.Me.GotAlivePet &&  Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(StyxWoW.Me.Pet.Location) < 10*10) > 1,
                    Pet.CreateCastPetAction("Felstorm")),
                new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.DistanceSqr < 10*10) > 1 && StyxWoW.Me.HealthPercent >= 70,
                    Spell.BuffSelf("Hellfire")
                    ),
                new Decorator(ret => StyxWoW.Me.CurrentTarget.IsBoss(),
                    new PrioritySelector(
                        Spell.BuffSelf("Metamorphosis"),
                        Spell.BuffSelf("Demon Soul"),
                        Spell.Cast("Immolation Aura", ret => StyxWoW.Me.CurrentTarget.Distance < 5f),
                        Spell.Cast("Shadowflame", ret => StyxWoW.Me.CurrentTarget.Distance < 5)
                        )),
                Spell.Buff("Immolate", true),
                Spell.Buff("Curse of Tongues", ret => StyxWoW.Me.CurrentTarget.PowerType == WoWPowerType.Mana),
                Spell.Buff("Curse of Elements", ret => StyxWoW.Me.CurrentTarget.PowerType != WoWPowerType.Mana),
                Spell.Buff("Bane of Doom", true, ret => StyxWoW.Me.CurrentTarget.IsBoss()),

                Spell.Buff("Bane of Agony", true, ret => !StyxWoW.Me.CurrentTarget.HasAura("Bane of Doom") && (StyxWoW.Me.CurrentTarget.HealthPercent >= 30 || StyxWoW.Me.CurrentTarget.Elite)),
                // Use the infernal if we have a few mobs around us, and it's off CD. Otherwise, just use the Doomguard.
                // Its a 10min CD, with a 1-1.2min uptime on the minion. Hey, any extra DPS is fine in my book!
                // Make sure these 2 summons are AFTER the banes above.
                new Decorator(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 10) > 2,
                    Spell.CastOnGround("Summon Infernal", ret => StyxWoW.Me.CurrentTarget.Location)
                    ),
                Spell.Cast("Summon Doomguard", ret=> StyxWoW.Me.CurrentTarget.IsBoss()),
                Spell.Buff("Corruption", true, ret => StyxWoW.Me.CurrentTarget.HealthPercent >= 30 || StyxWoW.Me.CurrentTarget.Elite),
                Spell.Cast("Drain Life", ret => StyxWoW.Me.HealthPercent < 70),
                Spell.Cast("Health Funnel", ret => StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && StyxWoW.Me.Pet.HealthPercent < 70),
                Spell.Cast("Hand of Gul'dan"),
                // TODO: Make this cast Soulburn if it's available
                Spell.Cast("Soul Fire", ret => StyxWoW.Me.HasAura("Improved Soul Fire") || StyxWoW.Me.HasAura("Soulburn")),
                //Spell.Cast("Soul Fire", ret => StyxWoW.Me.HasAura("Decimation")),
                Spell.Cast("Incinerate", ret => StyxWoW.Me.HasAura("Molten Core")),
                Spell.Cast("Shadow Bolt"),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
    }
}