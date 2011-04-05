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
using System.Diagnostics;

namespace Singular
{
    partial class SingularRoutine
    {
        private WoWItem ManaGem;
        public static Stopwatch SheepTimer = new Stopwatch();
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
                CreateSpellPartyBuff("Arcane Brilliance"),
                CreateSpellBuffOnSelf("Arcane Brilliance", ret => (!Me.HasAura("Arcane Brilliance") && !Me.HasAura("Fel Intelligence"))),
                CreateSpellBuffOnSelf(
                    "Molten Armor",
                    ret =>
                    !Me.HasAura("Molten Armor") && !Me.Mounted && !Me.IsFlying && !Me.IsOnTransport && !SpellManager.HasSpell("Mage Armor")),
                CreateSpellBuffOnSelf(
                    "Mage Armor",
                    ret =>
                    !Me.HasAura("Mage Armor") && !Me.Mounted && !Me.IsFlying && !Me.IsOnTransport && SpellManager.HasSpell("Molten Armor") &&
                    SpellManager.HasSpell("Frost Armor")),
                CreateSpellCast("Conjure Refreshment", ret => !Gotfood()),
                CreateSpellCast("Conjure Mana Gem", ret => !HaveManaGem() && SpellManager.HasSpell("Conjure Mana Gem")), //for dealing with managems
                new Decorator(
                    ret => TalentManager.CurrentSpec == TalentSpec.FrostMage && !Me.GotAlivePet,
                    new Action(ret => SpellManager.Cast("Summon Water Elemental")))
                );
        }
        public bool ColdSnapCheck()
        {
            List<int> SpellList = new List<int>();
            SpellList.Clear();
            //Check for Spells on Cooldown And Add to the List if they Are.
            if (SpellManager.HasSpell("Cone of Cold") && SpellManager.Spells["Cone of Cold"].Cooldown)
            {
                SpellList.Add(1);
            }
            if (SpellManager.HasSpell("Frost Nova") && SpellManager.Spells["Frost Nova"].Cooldown)
            {
                SpellList.Add(1);
            }
            if (SpellManager.HasSpell("Icy Veins") && SpellManager.Spells["Icy Veins"].Cooldown)
            {
                SpellList.Add(1);
            }
            if (SpellManager.HasSpell("Ice Barrier") && SpellManager.Spells["Ice Barrier"].Cooldown)
            {
                SpellList.Add(1);
            }
            if (SpellList.Count > 1)
            {
                return true;
            }
            return false;
        }
        public bool Gotfood()
        {
            return
                Me.BagItems.Where(item =>
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
        private static bool IsNotWanding
        {
            get
            {
                if (Lua.GetReturnVal<int>("return IsAutoRepeatSpell(\"Shoot\")", 0) == 1) { return false; }
                if (Lua.GetReturnVal<int>("return HasWandEquipped()", 0) == 0) { return false; }
                return true;
            }
        }





        public static bool Adds()
        {

            List<WoWUnit> mobList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
                unit.Guid != Styx.StyxWoW.Me.Guid &&
                unit.IsTargetingMeOrPet &&
                !unit.IsFriendly &&
                !Styx.Logic.Blacklist.Contains(unit.Guid));

            if (mobList.Count > 0)
            {
                return true;
            }
            return false;

        }
        public static bool HasSheeped()
        {
            List<WoWUnit> SheepedList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
            unit.Guid != Styx.StyxWoW.Me.Guid &&
            unit.Auras.ContainsKey("Polymorph"));
            if (SheepedList.Count > 0)
            {
                Logger.Write("Sheeped Target Detected disabling Frostnova");
                return true;
            }

            return false;

        }

        public static bool GotSheep
        {
            get
            {
                List<WoWUnit> sheepList =
                    (from o in ObjectManager.ObjectList
                     where o is WoWUnit
                     let p = o.ToUnit()
                     where p.Distance < 40
                           && p.HasAura("Polymorph")
                     select p).ToList();

                return sheepList.Count > 0;
            }
        }
        public static bool NeedToSheep()
        {
            if (!SpellManager.CanCast("Polymorph"))
            {
                return false;
            }
            List<WoWUnit> AddList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
  unit.Guid != Styx.StyxWoW.Me.Guid &&
  unit.IsTargetingMeOrPet &&
  !unit.IsFriendly &&
  !unit.IsTotem &&
  !unit.IsPet &&
  (unit.CreatureType == WoWCreatureType.Humanoid || unit.CreatureType == WoWCreatureType.Beast) &&
  unit != Styx.StyxWoW.Me.CurrentTarget &&
  !Styx.Logic.Blacklist.Contains(unit.Guid));
            if (AddList.Count > 0 && !GotSheep && Styx.StyxWoW.Me.CurrentTarget != null)
            {
                return true;
            }
            return false;

        }


        public static void SheepLogic()
        {
            List<WoWUnit> AddList = ObjectManager.GetObjectsOfType<WoWUnit>(false).FindAll(unit =>
unit.Guid != Styx.StyxWoW.Me.Guid &&
unit.IsTargetingMeOrPet &&
!unit.IsFriendly &&
!unit.IsPet &&
!unit.IsTotem &&
(unit.CreatureType == WoWCreatureType.Humanoid || unit.CreatureType == WoWCreatureType.Beast) &&
unit != Styx.StyxWoW.Me.CurrentTarget &&
!Styx.Logic.Blacklist.Contains(unit.Guid));

            if (AddList.Count > 0 && !GotSheep)
            {
                Logger.Write("ADDS! Slecting Poly Target");
                WoWUnit SheepAdd = AddList[0].ToUnit();
                Logger.Write("Casting Poly on " + SheepAdd.Name);
                SpellManager.Cast("Polymorph", SheepAdd);
                StyxWoW.SleepForLagDuration();
                SheepTimer.Reset();
                SheepTimer.Start();
            }
        }
    }
}