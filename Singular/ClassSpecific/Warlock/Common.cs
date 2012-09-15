﻿using System.Linq;
using System.Threading;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using CommonBehaviors.Actions;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.Warlock
{
    public class Common
    {
        private static bool NeedToCreateHealthStone
        {
            get
            {
                return !StyxWoW.Me.CarriedItems.Any(i => i.ItemSpells.Any(s => s.ActualSpell != null && s.ActualSpell.Name == "Healthstone"));
            }
        }

        private static bool NeedToCreateSoulStone
        {
            get
            {
                return !StyxWoW.Me.CarriedItems.Any(i => i.ItemSpells.Any(s => s.ActualSpell != null && s.ActualSpell.Name == "Soulstone Resurrection"));
            }
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                Spell.BuffSelf("Create Healthstone", ret => NeedToCreateHealthStone),
                Spell.BuffSelf("Create Soulstone", ret => NeedToCreateSoulStone),
                new Decorator(
                    ret => !StyxWoW.Me.HasAura("Soulstone Resurrection"),
                    new PrioritySelector(
                        ctx => Item.FindFirstUsableItemBySpell("Soulstone Resurrection"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logger.Write("Using soulstone on myself")),
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => StyxWoW.Me.ClearTarget()),
                                new Action(ret => ((WoWItem)ret).Use()),
                                new WaitContinue(System.TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()))))),
                //Spell.BuffSelf("Demon Armor", ret => !StyxWoW.Me.HasAura("Demon Armor") && !SpellManager.HasSpell("Fel Armor")),
                //Spell.BuffSelf("Fel Armor", ret => !StyxWoW.Me.HasAura("Fel Armor")),
                Spell.BuffSelf("Soul Link", ret => !StyxWoW.Me.HasAura("Soul Link") && StyxWoW.Me.GotAlivePet),
                Spell.BuffSelf("Health Funnel", ret => StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && StyxWoW.Me.Pet.HealthPercent < 60 && StyxWoW.Me.HealthPercent > 40)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Life Tap", ret => StyxWoW.Me.ManaPercent < 20 && StyxWoW.Me.HealthPercent > 40),
                Item.CreateUsePotionAndHealthstone(50, 10)
                );
        }

        [Behavior(BehaviorType.Rest, WoWClass.Warlock)]
        public static Composite CreateWarlockRest()
        {
            return new PrioritySelector(
                new Decorator(ctx => SingularSettings.Instance.DisablePetUsage && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => Lua.DoString("PetDismiss()"))),
                new Decorator(
                    ctx => StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name.Contains("Summon") && StyxWoW.Me.GotAlivePet,
                    new Action(ctx => SpellManager.StopCasting())),
                Spell.WaitForCast(false),
                Spell.BuffSelf("Life Tap", ret => StyxWoW.Me.ManaPercent < 80 && StyxWoW.Me.HealthPercent > 60 && !StyxWoW.Me.HasAnyAura("Drink", "Food")),
                new Decorator(ret=>(StyxWoW.Me.CurrentSoulShards <= 2 || StyxWoW.Me.HealthPercent <= 55) && !StyxWoW.Me.HasAnyAura("Drink", "Food"),
                    new PrioritySelector(
                        Spell.BuffSelf("Soul Harvest"),
                        new WaitContinue(System.TimeSpan.FromMilliseconds(500), ret => false, new ActionAlwaysSucceed()))),
                Rest.CreateDefaultRestBehaviour()
                );
        }
    }
}