

using System;
using System.Collections.Generic;
using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

using Action = TreeSharp.Action;
using Styx;

namespace Singular.ClassSpecific.Mage
{
    public class Common
    {
        private static WoWItem _manaGem;

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Spec(TalentSpec.FrostMage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public static Composite CreateMageBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Summon Water Elemental" && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                Waiters.WaitForCast(),
                Spell.BuffSelf("Arcane Brilliance", ret => (!StyxWoW.Me.HasAura("Arcane Brilliance") && !StyxWoW.Me.HasAura("Fel Intelligence"))),
                Spell.BuffSelf(
                    "Molten Armor",
                    ret =>
                    !StyxWoW.Me.HasAura("Molten Armor") && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsFlying && !StyxWoW.Me.IsOnTransport && !SpellManager.HasSpell("Mage Armor") &&
                    !SpellManager.HasSpell("Frost Armor")),
                Spell.BuffSelf(
                    "Frost Armor",
                    ret =>
                    !StyxWoW.Me.HasAura("Frost Armor") && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsFlying && !StyxWoW.Me.IsOnTransport && SpellManager.HasSpell("Molten Armor") &&
                    !SpellManager.HasSpell("Mage Armor")),
                Spell.BuffSelf(
                    "Mage Armor",
                    ret =>
                    !StyxWoW.Me.HasAura("Mage Armor") && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsFlying && !StyxWoW.Me.IsOnTransport && SpellManager.HasSpell("Molten Armor") &&
                    SpellManager.HasSpell("Frost Armor")),
                Spell.Cast("Conjure Refreshment", ret => !Gotfood()),
                Spell.Cast(759, ret => !HaveManaGem() && SpellManager.HasSpell(759)), //for dealing with managems
                new Decorator(
                    ret => TalentManager.CurrentSpec == TalentSpec.FrostMage && !StyxWoW.Me.GotAlivePet && SpellManager.CanCast("Summon Water Elemental"),
                    new Action(ret => SpellManager.Cast("Summon Water Elemental")))
                );
        }

        public static bool Gotfood()
        {
            return
                StyxWoW.Me.BagItems.Where(
                    item =>
                    item.Entry == 65500 || item.Entry == 65515 || item.Entry == 65516 || item.Entry == 65517 || item.Entry == 43518 ||
                    item.Entry == 43523 || item.Entry == 65499).Any();
        }

        private static WoWItem HaveItemCheck(List<int> listId)
        {
            return ObjectManager.GetObjectsOfType<WoWItem>(false).FirstOrDefault(item => listId.Contains(Convert.ToInt32(item.Entry)));
        }

        public static bool HaveManaGem()
        {
            foreach (WoWItem item in ObjectManager.GetObjectsOfType<WoWItem>(false).Where(item => item.Entry == 36799))
            {
                _manaGem = item;
                return true;
            }
            return false;
        }

        public static bool ManaGemNotCooldown()
        {
            if (_manaGem != null)
            {
                if (_manaGem.Cooldown == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static void UseManaGem()
        {
            if (_manaGem != null && ManaGemNotCooldown())
            {
                Lua.DoString("UseItemByName(\"" + _manaGem.Name + "\")");
            }
        }

        public static Composite CreateMagePolymorphOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != StyxWoW.Me.CurrentTarget &&
                            (u.CreatureType == WoWCreatureType.Beast || u.CreatureType == WoWCreatureType.Humanoid)),
                    new Decorator(
                        ret => ret != null,
                        Spell.Buff("Polymorph", ret => (WoWUnit)ret, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1)));
        }
    }
}
