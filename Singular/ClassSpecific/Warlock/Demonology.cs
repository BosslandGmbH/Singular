using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Styx;
using System.Linq;

namespace Singular.ClassSpecific.Warlock
{
    public class Demonology
    {
    

        #region Normal Rotation

        [Behavior(BehaviorType.Pull, WoWClass.Warlock, WoWSpec.WarlockDemonology, WoWContext.All)]
        public static Composite CreateWarlockDemonologyNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Spell.Buff("Corruption", true),
                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }
        [Behavior(BehaviorType.Combat, WoWClass.Warlock, WoWSpec.WarlockDemonology, WoWContext.All)]
        public static Composite CreateWarlockDemonologyNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),
                Helpers.Common.CreateAutoAttack(true),
                Helpers.Common.CreateInterruptSpellCast(ret => StyxWoW.Me.CurrentTarget),

                new Decorator(ret => StyxWoW.Me.CurrentTarget.Fleeing,
                    Pet.CreateCastPetAction("Axe Toss")),
                new Decorator(ret => StyxWoW.Me.GotAlivePet && Unit.NearbyUnfriendlyUnits.Count(u => u.Location.DistanceSqr(StyxWoW.Me.Pet.Location) < 10 * 10) > 1,
                    Pet.CreateCastPetAction("Felstorm")),



                new Decorator(ret => !StyxWoW.Me.ActiveAuras.ContainsKey("Molten Core") && StyxWoW.Me.CastingSpell != null && StyxWoW.Me.CastingSpell.Name == "Soul Fire", 
                    new Styx.TreeSharp.Action(r=>SpellManager.StopCasting())),
                new Decorator(ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Doom") || StyxWoW.Me.GetCurrentPower(WoWPowerType.DemonicFury) >= 900,
                    new ProbabilitySelector(

                        Spell.BuffSelf("Metamorphosis"),
                        //Spell.Cast("Doom")
                        new Decorator(ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Doom"), new Styx.TreeSharp.Action(r=>forcecast("Doom")))
                        )),


                new Decorator(ret=> StyxWoW.Me.HasAura("Metamorphosis"),
                    new PrioritySelector(
                        //Spell.Cast("Metamorphosis", ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.DemonicFury) < 800 && !StyxWoW.Me.HasAura("Dark Soul: Knowledge")),
                        new Decorator(ret => StyxWoW.Me.GetCurrentPower(WoWPowerType.DemonicFury) < 800 && !StyxWoW.Me.HasAura("Dark Soul: Knowledge"), new Styx.TreeSharp.Action(r => forcecast("Metamorphosis"))),
                        new Decorator(ret => SpellManager.CanCast("Dark Soul: Knowledge"), new Styx.TreeSharp.Action(r => forcecast("Dark Soul: Knowledge"))),
                        //Spell.Cast("Dark Soul: Knowledge"),
                        //Spell.Cast("Doom", ret => DoomTime < 5),
                        new Decorator(ret => DoomTime < 5, new Styx.TreeSharp.Action(r=>forcecast("Doom"))),
                        //Spell.Cast("Touch of Chaos")
                        new Styx.TreeSharp.Action(r => forcecast("Touch of Chaos"))


                    )),




                // Build demonic fury
                Spell.Buff("Corruption",true),
                Spell.Cast("Hand of Gul'dan", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Shadowflame")),
                Spell.Cast("Soul Fire", ret => StyxWoW.Me.ActiveAuras.ContainsKey("Molten Core")),
                Spell.Cast("Shadow Bolt"),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion





        static double DoomTime
        {
            get
            {
                var c = MyAura(StyxWoW.Me.CurrentTarget, "Doom");

                if (c == null)
                {
                    /*if (lastRuptureCP > 0)
                    {
                        lastRuptureCP = 0;
                        Logging.Write("Updating lastRuptureCP to 0");
                    }*/
                    return 0;
                }

                return c.TimeLeft.TotalSeconds;
            }
        }

        private static void forcecast(string what)
        {
            Logger.Write("Force casting "+ what);
            Lua.DoString("RunMacroText(\"/cast " + what + "\")");
        }


        private static WoWAura MyAura(WoWUnit Who, String What)
        {
            return Who.GetAllAuras().FirstOrDefault(p => p.CreatorGuid == StyxWoW.Me.Guid && p.Name == What);
        }
    }
}