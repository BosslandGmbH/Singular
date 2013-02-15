using System.Linq;
using System.Runtime.Remoting.Contexts;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;

using Styx;

using Styx.CommonBot;
using Styx.TreeSharp;

using Styx.Helpers;
using System;
using Styx.WoWInternals;
using Action = Styx.TreeSharp.Action;
using Styx.WoWInternals.WoWObjects;
using Styx.Common;
using System.Drawing;

namespace Singular.ClassSpecific.Warrior
{
    /// <summary>
    /// plaguerized from Apoc's simple Arms Warrior CC 
    /// see http://www.thebuddyforum.com/honorbuddy-forum/combat-routines/warrior/79699-arms-armed-quick-dirty-simple-fast.html#post815973
    /// </summary>
    public class Arms
    {

        #region Common

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings WarriorSettings { get { return SingularSettings.Instance.Warrior(); } }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateFaceTargetBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Helpers.Common.CreateDismount("Pulling"),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(),

                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Throw"),
                        Movement.CreateMoveToTargetBehavior(true, 27f)
                        )),

                //Buff up
                Spell.Cast(Common.SelectedShout),

                Common.CreateChargeBehavior(),

                // Move to Melee
                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion

        #region Normal

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalCombatBuffs()
        {
            return new Throttle( 
                new Decorator( 
                        ret => !Spell.IsGlobalCooldown()
                        && Me.CurrentTarget != null 
                        && Me.CurrentTarget.IsWithinMeleeRange,

                    new PrioritySelector(
                        Spell.BuffSelf("Battle Stance"),

                        Spell.Cast("Recklessness", ret => (SpellManager.CanCast("Execute") || Common.Tier14FourPieceBonus) && (StyxWoW.Me.CurrentTarget.Elite || StyxWoW.Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances)),
                        Spell.Cast("Skull Banner", ret => Me.CurrentTarget.IsBoss()),

                        Spell.Cast("Avatar", ret => Me.CurrentTarget.IsBoss()),
                        Spell.Cast("Bloodbath", ret => Me.CurrentTarget.IsBoss()),
                        // Spell.Cast("Storm Bolt"),  // in normal rotation

                        Spell.Cast("Deadly Calm", ret => StyxWoW.Me.HasAura("Taste for Blood")),

                        // Execute is up, so don't care just cast
                        Spell.Cast("Berserker Rage", ret => Me.CurrentTarget.HealthPercent <= 20),
                        // May get an Enrage off Mortal Strike + Colossus Smash pair, so try to avoid overlapping Enrages
                        Spell.Cast("Berserker Rage", ret => !Me.ActiveAuras.ContainsKey("Enrage") && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds > 6)
                        )
                    )
                );
        }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms, WoWContext.All)]
        public static Composite CreateArmsNormalCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(false),

                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown() && !StyxWoW.Me.HasAura( "Bladestorm"),

                    new PrioritySelector(
                        
                        CreateDiagnosticOutputBehavior(),

                        Helpers.Common.CreateInterruptBehavior(),

                        Common.CreateVictoryRushBehavior(),

                        Spell.Buff("Piercing Howl", ret => Me.CurrentTarget.Distance < 10 && Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),
                        Spell.Buff("Hamstring", ret => Me.CurrentTarget.IsPlayer && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamstring") && SingularSettings.Instance.Warrior().UseWarriorSlows),

                        CreateArmsAoeCombat(ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance < (u.MeleeDistance() + 1))),

#region EXECUTE AVAILABLE
                        new Decorator( ret => Me.CurrentTarget.HealthPercent <= 20,
                            new PrioritySelector(
                                Spell.Cast("Colossus Smash"),
                                Spell.Cast("Execute"),
                                Spell.Cast("Mortal Strike"),
                                Spell.Cast("Overpower"),
                                Spell.Cast("Storm Bolt"),
                                Spell.Cast("Dragon Roar", ret => (Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances) && (Me.CurrentTarget.Distance <= 8 || Me.CurrentTarget.IsWithinMeleeRange)),
                                Spell.Cast("Slam"),
                                Spell.Cast("Battle Shout"))
                            ),
#endregion

#region EXECUTE NOT AVAILABLE
                        new Decorator(ret => Me.CurrentTarget.HealthPercent > 20,
                            new PrioritySelector(
                                // Only drop DC if we need to use HS for TFB. This lets us avoid breaking HS as a rage dump, when we don't want it to be one.
                                Spell.Cast("Deadly Calm", ret => NeedTasteForBloodDump),

                                // currently makes more sense to burn through a target, so HS instead of Cleave
                                // Spell.Cast("Cleave", ret => NeedHeroicStrikeDump && Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) > 1),
                                Spell.Cast("Heroic Strike", ret => NeedHeroicStrikeDump ),

                                Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash")),
                                Spell.Cast("Execute"),
                                Spell.Cast("Mortal Strike"),

                                //HeroicLeap(),

                                Spell.Cast("Storm Bolt"),
                                Spell.Cast("Dragon Roar", ret => (Me.CurrentTarget.IsBoss() || SingularRoutine.CurrentWoWContext != WoWContext.Instances) && (Me.CurrentTarget.Distance <= 8 || Me.CurrentTarget.IsWithinMeleeRange)),
                                Spell.Cast("Overpower"),

                                // Rage dump!
                                Spell.Cast("Slam", ret => (StyxWoW.Me.RagePercent >= 60 || StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash")) && StyxWoW.Me.CurrentTarget.HealthPercent > 20)

                                )
                            ),
#endregion

                        Common.CreateChargeBehavior(),

                        Spell.Cast( "Battle Shout", ret => StyxWoW.Me.CurrentRage < 70 )
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }


        private static Composite CreateArmsAoeCombat(SimpleIntDelegate aoeCount)
        {
            return new PrioritySelector(
                new Decorator(ret => Spell.UseAOE && aoeCount(ret) >= 3,
                    new PrioritySelector(
                        Spell.Cast( "Thunder Clap" ),

                        Spell.Cast("Bladestorm", ret => aoeCount(ret) >= 4),
                        Spell.Cast("Shockwave", ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 10f) >= 3),
                        Spell.Cast("Dragon Roar"),

                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Mortal Strike"),
                        Spell.Cast("Colossus Smash", ret => !StyxWoW.Me.CurrentTarget.HasMyAura("Colossus Smash")),
                        Spell.Cast("Overpower")
                        )
                    ),

                Spell.BuffSelf("Sweeping Strikes", ret => aoeCount(ret) == 2)
                );
        }


        Composite HeroicLeap()
        {
            return new Decorator(ret => StyxWoW.Me.CurrentTarget.HasAura("Colossus Smash") && SpellManager.CanCast("Heroic Leap"),
                new Action(ret =>
                {
                    var tpos = StyxWoW.Me.CurrentTarget.Location;
                    var trot = StyxWoW.Me.CurrentTarget.Rotation;
                    var leapRight = WoWMathHelper.CalculatePointAtSide(tpos, trot, 5, true);
                    var leapLeft = WoWMathHelper.CalculatePointAtSide(tpos, trot, 5, true);


                    var myPos = StyxWoW.Me.Location;


                    var leftDist = leapLeft.Distance(myPos);
                    var rightDist = leapRight.Distance(myPos);


                    var leapPos = WoWMathHelper.CalculatePointBehind(tpos, trot, 8);


                    if (leftDist > rightDist && leftDist <= 40 && leftDist >= 8)
                        leapPos = leapLeft;
                    else if (rightDist > leftDist && rightDist <= 40 && rightDist >= 8)
                        leapPos = leapLeft;


                    SpellManager.Cast("Heroic Leap");
                    SpellManager.ClickRemoteLocation(leapPos);
                    StyxWoW.Me.CurrentTarget.Face();
                }));
        }


        private static void UseTrinkets()
        {
            var firstTrinket = StyxWoW.Me.Inventory.Equipped.Trinket1;
            var secondTrinket = StyxWoW.Me.Inventory.Equipped.Trinket2;
            var hands = StyxWoW.Me.Inventory.Equipped.Hands;

            if (firstTrinket != null && CanUseEquippedItem(firstTrinket))
                firstTrinket.Use();


            if (secondTrinket != null && CanUseEquippedItem(secondTrinket))
                secondTrinket.Use();

            if (hands != null && CanUseEquippedItem(hands))
                hands.Use();

        }
        private static bool CanUseEquippedItem(WoWItem item)
        {
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")", 0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown <= 0;
        }


        static bool NeedTasteForBloodDump
        {
            get
            {
                var tfb = Me.GetAllAuras().FirstOrDefault(a => a.Name == "Taste for Blood" && a.TimeLeft > TimeSpan.Zero && a.StackCount > 0);
                if (tfb != null)
                {
                    // If we have more than 3 stacks, pop HS
                    if (tfb.StackCount >= 3)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood");
                        return true;
                    }

                    // If it's about to drop, and we have at least 2 stacks, then pop HS.
                    // If we have 1 stack, then a slam is better used here.
                    if (tfb.TimeLeft.TotalSeconds < 1 && tfb.StackCount >= 2)
                    {
                        Logger.WriteDebug(Color.White, "^Taste for Blood (falling off)");
                        return true;
                    }
                }
                return false;
            }
        }

        static bool NeedHeroicStrikeDump
        {
            get
            {
                // Flat out, drop HS if we need to.
                if (StyxWoW.Me.RagePercent >= 85)
                {
                    Logger.Write(Color.White, "^Rage Dump: Heroic Strike");
                    return true;
                }

                return NeedTasteForBloodDump;
            }
        }


        private static Composite CreateDiagnosticOutputBehavior()
        {
            return new ThrottlePasses( 1,
                new Decorator(
                    ret => SingularSettings.Debug,
                    new Action(ret =>
                        {
                        WoWUnit target = Me.CurrentTarget ?? Me;
                        Logger.Write( Color.Yellow, ".... h={0:F1}%/r={1:F1}%, Enrage={2} Coloss={3} MortStrk={4}",
                            Me.HealthPercent,
                            Me.CurrentRage,
                            Me.ActiveAuras.ContainsKey("Enrage"),
                            Spell.GetSpellCooldown("Colossus Smash").TotalSeconds,
                            Spell.GetSpellCooldown("Mortal Strike").TotalSeconds
                            );
                        return RunStatus.Failure;
                        })
                    )
                );
        }
        #endregion
    }
}