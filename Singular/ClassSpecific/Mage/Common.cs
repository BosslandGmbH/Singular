using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

using Action = TreeSharp.Action;
using Styx;

namespace Singular.ClassSpecific.Mage
{
    public static class Common
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
                Spell.WaitForCast(),

                Spell.BuffSelf("Arcane Brilliance", ret => (!StyxWoW.Me.HasAura("Arcane Brilliance") && !StyxWoW.Me.HasAura("Fel Intelligence"))),

                // Additional armors/barriers for BGs. These should be kept up at all times to ensure we're as survivable as possible.
                new Decorator(
                    ret => (SingularRoutine.CurrentWoWContext & WoWContext.Battlegrounds) != 0,
                    new PrioritySelector(
                        // FA in BGs all the time. Damage reduction is win, and so is the slow. Serious PVPers will have this glyphed too, for the 2% mana regen.
                        Spell.BuffSelf("Frost Armor"),
                        // Mage ward up, at all times. Period.
                        Spell.BuffSelf("Mage Ward"),
                        // Don't put up mana shield if we're arcane. Since our mastery works off of how much mana we have!
                        Spell.BuffSelf("Mana Shield", ret => TalentManager.CurrentSpec != TalentSpec.ArcaneMage))),

                // We may not have it, but if we do, it should be up 100% of the time.
                Spell.BuffSelf("Ice Barrier"),

                // Outside of BGs, we really only have 2 choices of armor. Molten, or mage. Mage for arcane, molten for frost/fire.
                new Decorator(
                    ret => (SingularRoutine.CurrentWoWContext & WoWContext.Battlegrounds) == 0,
                    new PrioritySelector(
                        // Arcane is a mana whore, we want molten if we don't have mage yet. Otherwise, stick with Mage armor.
                        Spell.BuffSelf("Molten Armor", ret => (TalentManager.CurrentSpec != TalentSpec.ArcaneMage || !SpellManager.HasSpell("Mage Armor"))),
                        Spell.BuffSelf("Mage Armor", ret => TalentManager.CurrentSpec == TalentSpec.ArcaneMage))),

                Spell.Cast("Conjure Refreshment", ret => !Gotfood()),
                Spell.Cast(759, ret => !HaveManaGem() && SpellManager.HasSpell(759)), //for dealing with managems
                new Decorator(
                    ret =>
                    TalentManager.CurrentSpec == TalentSpec.FrostMage && !StyxWoW.Me.GotAlivePet && SpellManager.CanCast("Summon Water Elemental"),
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

        public static Composite CreateStayAwayFromFrozenTargetsBehavior()
        {
            return
                new PrioritySelector(ctx => 
                    Unit.NearbyUnfriendlyUnits.Where(u => u.HasAura("Frost Nova") || u.HasAura("Freeze")).
                                               OrderBy(u => u.DistanceSqr).FirstOrDefault(),
                    new Decorator(
                        ret => ret != null && !SingularSettings.Instance.DisableAllMovement,
                        new PrioritySelector(
                            new Decorator(
                                ret => ((WoWUnit)ret).Distance < Spell.SafeMeleeRange + 3f,
                                new Action(ret =>
                                               {
                                                   WoWPoint moveTo =
                                                       WoWMathHelper.CalculatePointBehind(
                                                           ((WoWUnit)ret).Location,
                                                           ((WoWUnit)ret).Rotation,
                                                           -(Spell.SafeMeleeRange + 5f));

                                                   if (Navigator.CanNavigateFully(StyxWoW.Me.Location, moveTo))
                                                   {
                                                       Logger.Write("Getting away from frozen target");
                                                       Navigator.MoveTo(moveTo);
                                                       return RunStatus.Success;
                                                   }

                                                   return RunStatus.Failure;
                                               })),
                            Movement.CreateEnsureMovementStoppedBehavior())
                        ));
        }

        public static Composite CreateMagePolymorphOnAddBehavior()
        {
            return 
                new PrioritySelector(
                    ctx => Unit.NearbyUnfriendlyUnits.OrderByDescending(u => u.CurrentHealth).FirstOrDefault(IsViableForPolymorph),
                    new Decorator(
                        ret => ret != null && Unit.NearbyUnfriendlyUnits.All(u => !u.HasMyAura("Polymorph")),
                        new PrioritySelector(
                            Spell.PreventDoubleCast(ret => (WoWUnit)ret, "Polymorph"),
                            Spell.Buff("Polymorph", ret => (WoWUnit)ret))));
        }

        private static bool IsViableForPolymorph(WoWUnit unit)
        {
            if (unit.IsCrowdControlled())
                return false;

            if (unit.CreatureType != WoWCreatureType.Beast && unit.CreatureType != WoWCreatureType.Humanoid)
                return false;

            if (StyxWoW.Me.CurrentTarget != null && StyxWoW.Me.CurrentTarget == unit)
                return false;

            if (!unit.Combat)
                return false;

            if (!unit.IsTargetingMeOrPet && !unit.IsTargetingMyPartyMember)
                return false;

            if (StyxWoW.Me.IsInParty && StyxWoW.Me.PartyMembers.Any(p => p.CurrentTarget != null && p.CurrentTarget == unit))
                return false;

            return true;
        }
    }
}
