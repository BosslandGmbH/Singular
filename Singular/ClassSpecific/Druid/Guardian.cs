using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace Singular.ClassSpecific.Druid
{
    class Guardian
    {
        private static DruidSettings Settings
        {
            get { return SingularSettings.Instance.Druid(); }
        }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        #region Common

        [Behavior(BehaviorType.Pull, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),
                //Dismount
                Helpers.Common.CreateDismount("Pulling"),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                    //Shoot flying targets
                    new Decorator(
                        ret => Me.CurrentTarget.IsFlying,
                        new PrioritySelector(
                            Spell.Cast("Moonfire"),
                            Movement.CreateMoveToTargetBehavior(true, 27f)
                            )),

                        Spell.BuffSelf("Bear Form"),
                        Spell.Cast("Faerie Fire")
                        )
                    ),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalPreCombatBuffs()
        {
            return new PrioritySelector();
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All, 1)]
        public static Composite CreateGuardianNormalCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Bear Form"),

                // Enrage ourselves back up to 60 rage for SD/FR usage.
                Spell.BuffSelf("Enrage", ret=>StyxWoW.Me.RagePercent <= 40),

                // Symbiosis
                Common.SymbCast(Symbiosis.BoneShield, on => Me, ret => !Me.HasAura("Bone Shield")),
                Common.SymbCast(Symbiosis.ElusiveBrew, on => Me, ret => StyxWoW.Me.HealthPercent <= 60 && !Me.HasAura("Elusive Brew")),
                Common.SymbCast(Symbiosis.SpellReflection, on => Me, ret => !Me.HasAura("Bone Shield") && Unit.NearbyUnfriendlyUnits.Any(u => u.IsCasting && u.CurrentTargetGuid == Me.Guid && u.CurrentCastTimeLeft.TotalSeconds < 3)),

                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < Settings.TankFrenziedRegenerationHealth && Me.CurrentRage >=60),
                Spell.BuffSelf("Frenzied Regeneration", ret => Me.HealthPercent < 30 && Me.CurrentRage >= 15),
                Spell.BuffSelf("Savage Defense", ret => Me.HealthPercent < Settings.TankSavageDefense),
                Spell.BuffSelf("Might of Ursoc", ret => Me.HealthPercent < Settings.TankMightOfUrsoc),
                Spell.BuffSelf("Survival Instincts", ret => Me.HealthPercent < Settings.TankSurvivalInstinctsHealth),
                Spell.BuffSelf("Barkskin", ret => Me.HealthPercent < Settings.TankFeralBarkskin),
                Spell.Cast("Renewal", on => Me, ret => Me.HealthPercent < Settings.RenewalHealth)

                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.All)]
        public static Composite CreateGuardianNormalCombat()
        {
           // Logger.Write("guardian loop.");
            return new PrioritySelector(
                //Ensure Target
                Safers.EnsureTarget(),
                //LOS check
                Movement.CreateMoveToLosBehavior(),
                // face target
                Movement.CreateFaceTargetBehavior(),
                // Auto Attack
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Helpers.Common.CreateInterruptBehavior(),
                        
                        Spell.Cast("Mangle"),
                        Spell.Cast("Thrash"),

                        Spell.Cast("Bear Hug", 
                            ret => SingularRoutine.CurrentWoWContext != WoWContext.Instances
                                && !Me.HasAura("Berserk") 
                                && !Unit.NearbyUnfriendlyUnits.Any(u => u.Guid != Me.CurrentTargetGuid && u.CurrentTargetGuid == Me.Guid)),

                        new Decorator(
                            ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < 8) >= 2,
                            new PrioritySelector(
                                Spell.Cast("Berserk"),
                                Spell.Cast("Swipe")
                                )
                            ),
                        Spell.Cast("Lacerate"),
                        Spell.Buff("Faerie Fire"),
                        Spell.Cast("Maul", ret=> Me.CurrentRage >= 90 && StyxWoW.Me.HasAura("Tooth and Claw")),

                        // Symbiosis
                        Common.SymbCast(Symbiosis.Consecration, on => Me, req => Me.CurrentTarget.SpellDistance() < 8)
                        )
                    ),                   

                Movement.CreateMoveToMeleeBehavior(true)
            );
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian, WoWContext.Battlegrounds | WoWContext.Instances, 2)]
        public static Composite CreateGuardianPreCombatBuffForSymbiosis(UnitSelectionDelegate onUnit)
        {
            return Common.CreateDruidCastSymbiosis(on => GetGuardianBestSymbiosisTarget());
        }

        private static WoWPlayer GetGuardianBestSymbiosisTarget()
        {
            return Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.DeathKnight)
                ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Paladin)
                    ?? (Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Warrior)
                        ?? Unit.NearbyGroupMembers.FirstOrDefault(p => Common.IsValidSymbiosisTarget(p) && p.Class == WoWClass.Monk)));
        }

    }
}