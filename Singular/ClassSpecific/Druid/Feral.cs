#region

using System;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

#endregion

namespace Singular.ClassSpecific.Druid
{
    public class Feral
    {
        private static DruidSettings DruidSettings
        {
            get { return SingularSettings.Instance.Druid; }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateAutoAttack(false),
                new Decorator(ret => Me.Mounted,
                              Helpers.Common.CreateDismount("Pulling")),

                Spell.WaitForCast(),

                new Decorator(
                    ret => !SpellManager.GlobalCooldown, 
                    new PrioritySelector(

                        //Shoot flying targets
                        new Decorator(
                            ret => Me.CurrentTarget.IsFlying,
                            new PrioritySelector(
                                Spell.Buff("Faerie Fire", ret => Me.CurrentTarget.Distance < 35),
                                Spell.Cast("Moonfire", ret => Me.CurrentTarget.Distance < 40),
                                Movement.CreateMoveToTargetBehavior(true, 27f)
                                )),

                        Spell.Buff("Prowl", ret => !Me.Combat ),
                        Spell.Cast("Pounce", ret => Me.HasAura("Prowl") && Me.CurrentTarget.IsWithinMeleeRange ),
                        Spell.Buff("Rake", ret => Me.CurrentTarget.IsWithinMeleeRange ),
                        Spell.Cast("Mangle", ret => Me.CurrentTarget.IsWithinMeleeRange )
                        )
                    ),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Cat Form"));
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral, WoWContext.All)]
        public static Composite CreateFeralNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(), 
                    new PrioritySelector(

                        // updated time to death tracking values before we need them
                        new Action( ret => { Me.CurrentTarget.TimeToDeath(); return RunStatus.Failure; } ),

                        TimeToDeathExtension.CreateWriteDebugTimeToDeath(),

                        //Single target
                        Spell.Cast("Faerie Fire", ret =>!Me.CurrentTarget.HasAura("Weakened Armor", 3)),

                        new Throttle( Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar") && (Me.ComboPoints > 1 || TalentManager.HasGlyph("Savagery")))),

                        new Throttle( Spell.BuffSelf("Tiger's Fury", 
                                   ret => Common.energy <= 35 
                                       && !Me.ActiveAuras.ContainsKey("Clearcasting")
                                       && !Me.HasAura("Berserk")
                                       )),

                        new Throttle( Spell.BuffSelf("Berserk", ret => Me.HasAura("Tiger's Fury") && (Me.CurrentTarget.IsBoss || Me.CurrentTarget.IsPlayer ))),
                        new Throttle( Spell.Cast("Nature's Vigil", ret => Me.HasAura("Berserk"))),
                        Spell.Cast("Incarnation", ret => Me.HasAura("Berserk")),

                        // bite if rip good for awhile or target dying soon
                        Spell.Cast("Ferocious Bite", 
                            ret => Me.ComboPoints >= 5
                                && (Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds > 6 || Me.CurrentTarget.TimeToDeath() < 6)),

                        Spell.Cast("Rip",
                            ret => Me.ComboPoints >= 5
                                && Me.CurrentTarget.TimeToDeath() >= 7
                                && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds < 1),

                        Spell.Cast("Ravage"),

                        Spell.Buff("Rake", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake", true).TotalSeconds < 3),

                        Spell.Cast("Shred", 
                            ret =>  (Me.CurrentTarget.MeIsSafelyBehind || (TalentManager.HasGlyph("Shred") && (Me.HasAnyAura("Tiger's Fury", "Berserk"))))),

                        Spell.Cast("Mangle"),

                        Spell.CastOnGround("Force of Nature", 
                            u => (Me.CurrentTarget ?? Me) .Location,
                            ret => StyxWoW.Me.CurrentTarget != null 
                                && StyxWoW.Me.CurrentTarget.Distance < 40
                                && SpellManager.HasSpell("Force of Nature"))
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

    }
}