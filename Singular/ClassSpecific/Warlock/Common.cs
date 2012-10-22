using System.Linq;
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
    
        private static bool HaveHealthStone { get { return StyxWoW.Me.BagItems.Any(i => i.Entry == 5512); } }
        //5512 - healthstone
        private static bool NeedToCreateSoulStone
        {
            get
            {
                return !StyxWoW.Me.CarriedItems.Any(i => i.ItemSpells.Any(s => s.ActualSpell != null && s.ActualSpell.Name == "Soulstone Resurrection"));
            }
        }
        public static Composite UseHealthStoneBehavior() { return UseHealthStoneBehavior(ret => true); }

        public static Composite UseHealthStoneBehavior(SimpleBooleanDelegate requirements)
        {
            return new PrioritySelector(
                ctx => StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == 5512),
                new Decorator(
                    ret => ret != null && StyxWoW.Me.HealthPercent < 100 && ((WoWItem)ret).Cooldown == 0 && requirements(ret),
                    new Sequence(
                        new Action(ret => Logger.Write("Using {0}", ((WoWItem)ret).Name)),
                        new Action(ret => ((WoWItem)ret).Use())))
                );
        }
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warlock)]
        public static Composite CreateWarlockPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.WaitForCast(true),
                Spell.BuffSelf("Create Healthstone", ret => !HaveHealthStone),
                Spell.BuffSelf("Soulstone", ret => !StyxWoW.Me.HasAura("Soulstone")),
                Spell.Buff("Dark Intent", ret => !StyxWoW.Me.HasAura("Dark Intent")),
                new Decorator(ret => !StyxWoW.Me.GotAlivePet,
                new Switch<WoWSpec>(ctx => StyxWoW.Me.Specialization,
                                            new SwitchArgument<WoWSpec>(WoWSpec.None, 
                                                new PrioritySelector(
                                                new Decorator(ret => SpellManager.HasSpell("Summon Voidwalker"),
                                                    new Action(ret => PetManager.CallPet("Voidwalker"))),
                                                new Decorator(ret => !SpellManager.HasSpell("Summon Voidwalker"),
                                                    new Action(ret => PetManager.CallPet("Imp"))))),
                                            new SwitchArgument<WoWSpec>(WoWSpec.WarlockAffliction, 
                                                new PrioritySelector(
                                                new Decorator(ret => SpellManager.HasSpell("Summon Felhunter"),
                                                    new Action(ret => PetManager.CallPet("Felhunter"))),
                                                new Decorator(ret => !SpellManager.HasSpell("Summon Felhunter"),
                                                    new Action(ret => PetManager.CallPet("Voidwalker"))))),
                                            new SwitchArgument<WoWSpec>(WoWSpec.WarlockDemonology,
                                                new PrioritySelector(
                                                new Decorator(ret => SpellManager.HasSpell("Summon Felguard"),
                                                    new Action(ret => PetManager.CallPet("Felguard"))),
                                                new Decorator(ret => !SpellManager.HasSpell("Summon Felguard"),
                                                    new Action(ret => PetManager.CallPet("Voidwalker"))))),
                                            new SwitchArgument<WoWSpec>(WoWSpec.WarlockDestruction,  
                                                new PrioritySelector(
                                                new Decorator(ret => SpellManager.HasSpell("Summon Felhunter"),
                                                    new Action(ret => PetManager.CallPet("Felhunter"))),
                                                new Decorator(ret => !SpellManager.HasSpell("Summon Felhunter"),
                                                    new Action(ret => PetManager.CallPet("Voidwalker")))))
                                            ))
                                        
                //Spell.BuffSelf("Health Funnel", ret => StyxWoW.Me.GotAlivePet && PetManager.PetTimer.IsFinished && StyxWoW.Me.Pet.HealthPercent < 60 && StyxWoW.Me.HealthPercent > 40)
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
                UseHealthStoneBehavior(ret => StyxWoW.Me.HealthPercent < 80),
                Rest.CreateDefaultRestBehaviour()
                );
        }
    }
}