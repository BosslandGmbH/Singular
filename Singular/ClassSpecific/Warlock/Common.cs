using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using TreeSharp;

namespace Singular.ClassSpecific.Warlock
{
    partial class SingularRoutine
    {
        private static string WantedPet { get; set; }

        private static bool NeedToCreateHealthStone
        {
            get
            {
                return !StyxWoW.Me.CarriedItems.Any(i => i.ItemSpells.Any(s => s.ActualSpell != null && s.ActualSpell.Name == "Healthstone"));
            }
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Waiters.WaitForCast(true),
                Spell.BuffSelf("Create Healthstone", ret => NeedToCreateHealthStone),
                Spell.BuffSelf("Demon Armor", ret => !StyxWoW.Me.HasAura("Demon Armor") && !SpellManager.HasSpell("Fel Armor")),
                Spell.BuffSelf("Fel Armor", ret => !StyxWoW.Me.HasAura("Fel Armor")),
                Spell.BuffSelf("Soul Link", ret => !StyxWoW.Me.HasAura("Soul Link") && StyxWoW.Me.GotAlivePet),
                Pet.CreateSummonPet(WantedPet),
                Spell.BuffSelf("Health Funnel", ret => StyxWoW.Me.GotAlivePet && StyxWoW.Me.Pet.HealthPercent < 60 && StyxWoW.Me.HealthPercent > 40)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(
                Item.CreateUsePotionAndHealthstone(50, 10)
                );
        }

        [Class(WoWClass.Warlock)]
        [Spec(TalentSpec.AfflictionWarlock)]
        [Spec(TalentSpec.DemonologyWarlock)]
        [Spec(TalentSpec.DestructionWarlock)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.Rest)]
        [Context(WoWContext.All)]
        public static Composite CreateWarlockRest()
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Summon " + WantedPet && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                Waiters.WaitForCast(true),
                Spell.BuffSelf("Life Tap", ret => StyxWoW.Me.ManaPercent < 80 && StyxWoW.Me.HealthPercent > 40),
                Spell.BuffSelf("Soul Harvest", ret => StyxWoW.Me.CurrentSoulShards < 2 || StyxWoW.Me.HealthPercent <= 55),
                Rest.CreateDefaultRestBehaviour()
                );
        }
    }
}