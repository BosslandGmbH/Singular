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

using System;
using System.Collections.Generic;
using System.Linq;

using Styx.Combat.CombatRoutine;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;

using Action = TreeSharp.Action;
using Styx;

namespace Singular
{
    partial class SingularRoutine
    {
        private WoWItem ManaGem;

        [Class(WoWClass.Mage)]
        [Spec(TalentSpec.FireMage)]
        [Spec(TalentSpec.FrostMage)]
        [Spec(TalentSpec.ArcaneMage)]
        [Spec(TalentSpec.Lowbie)]
        [Behavior(BehaviorType.PreCombatBuffs)]
        [Context(WoWContext.All)]
        public Composite CreateMageBuffs()
        {
            return new PrioritySelector(
                new Decorator(
                    ctx => Me.CastingSpell != null && Me.CastingSpell.Name == "Summon Water Elemental" && Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                CreateWaitForCast(),
                CreateSpellBuffOnSelf("Arcane Brilliance", ret => (!Me.HasAura("Arcane Brilliance") && !Me.HasAura("Fel Intelligence"))),
                CreateSpellBuffOnSelf(
                    "Molten Armor",
                    ret =>
                    !Me.HasAura("Molten Armor") && !Me.Mounted && !Me.IsFlying && !Me.IsOnTransport && !SpellManager.HasSpell("Mage Armor") &&
                    !SpellManager.HasSpell("Frost Armor")),
                CreateSpellBuffOnSelf(
                    "Frost Armor",
                    ret =>
                    !Me.HasAura("Frost Armor") && !Me.Mounted && !Me.IsFlying && !Me.IsOnTransport && SpellManager.HasSpell("Molten Armor") &&
                    !SpellManager.HasSpell("Mage Armor")),
                CreateSpellBuffOnSelf(
                    "Mage Armor",
                    ret =>
                    !Me.HasAura("Mage Armor") && !Me.Mounted && !Me.IsFlying && !Me.IsOnTransport && SpellManager.HasSpell("Molten Armor") &&
                    SpellManager.HasSpell("Frost Armor")),
                CreateSpellCast("Conjure Refreshment", ret => !Gotfood()),
                CreateSpellCast(759, ret => !HaveManaGem() && SpellManager.HasSpell(759)), //for dealing with managems
                new Decorator(
                    ret => TalentManager.CurrentSpec == TalentSpec.FrostMage && !Me.GotAlivePet,
                    new Action(ret => SpellManager.Cast("Summon Water Elemental")))
                );
        }

        public bool Gotfood()
        {
            return
                Me.BagItems.Where(
                    item =>
                    item.Entry == 65500 || item.Entry == 65515 || item.Entry == 65516 || item.Entry == 65517 || item.Entry == 43518 ||
                    item.Entry == 43523 || item.Entry == 65499).Any();
        }

        private WoWItem HaveItemCheck(List<int> listId)
        {
            foreach (WoWItem item in ObjectManager.GetObjectsOfType<WoWItem>(false))
            {
                if (listId.Contains(Convert.ToInt32(item.Entry)))
                {
                    return item;
                }
            }
            return null;
        }

        public bool HaveManaGem()
        {
            foreach (WoWItem item in ObjectManager.GetObjectsOfType<WoWItem>(false))
            {
                if (item.Entry == 36799)
                {
                    ManaGem = item;
                    return true;
                }
            }
            return false;
        }

        public bool ManaGemNotCooldown()
        {
            if (ManaGem != null)
            {
                if (ManaGem.Cooldown == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void UseManaGem()
        {
            if (ManaGem != null && ManaGemNotCooldown())
            {
                Lua.DoString("UseItemByName(\"" + ManaGem.Name + "\")");
            }
        }

        protected Composite CreateMagePolymorphOnAddBehavior()
        {
            return new PrioritySelector(
                    ctx => NearbyUnfriendlyUnits.FirstOrDefault(u =>
                            u.IsTargetingMeOrPet && u != Me.CurrentTarget &&
                            (u.CreatureType == WoWCreatureType.Beast || u.CreatureType == WoWCreatureType.Humanoid)),
                    new Decorator(
                        ret => ret != null,
                        CreateSpellBuff("Polymorph", ret => NearbyUnfriendlyUnits.Count(u => u.Aggro) > 1, ret => (WoWUnit)ret, true)));
        }
    }
}