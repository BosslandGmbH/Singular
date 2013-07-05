//#define SHOW_BEHAVIOR_LOAD_DESCRIPTION
//#define BOTS_NOT_CALLING_PULLBUFFS
//#define TESTING_WHILE_IN_VEHICLE_COMPLETED

using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;
using Styx.TreeSharp;
using Singular.ClassSpecific;
using System.Drawing;
using CommonBehaviors.Actions;
using Styx.Common;
using System;

using Action = Styx.TreeSharp.Action;
using Styx.CommonBot;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.Common.Helpers;
using System.Collections.Generic;

namespace Singular
{
    partial class SingularRoutine
    {
        private Composite _combatBehavior;
        private Composite _combatBuffsBehavior;
        private Composite _healBehavior;
        private Composite _preCombatBuffsBehavior;
        private Composite _pullBehavior;
        private Composite _pullBuffsBehavior;
        private Composite _restBehavior;
        private Composite _lostControlBehavior;
        public override Composite CombatBehavior { get { return _combatBehavior; } }
        public override Composite CombatBuffBehavior { get { return _combatBuffsBehavior; } }
        public override Composite HealBehavior { get { return _healBehavior; } }
        public override Composite PreCombatBuffBehavior { get { return _preCombatBuffsBehavior; } }
        public override Composite PullBehavior { get { return _pullBehavior; } }
        public override Composite PullBuffBehavior { get { return _pullBuffsBehavior; } }
        public override Composite RestBehavior { get { return _restBehavior; } }

        private static ulong _guidLastTarget = 0;
        private static WaitTimer _timerLastTarget = new WaitTimer(TimeSpan.FromSeconds(5));

        public bool RebuildBehaviors(bool silent = false)
        {
            Logger.PrintStackTrace("RebuildBehaviors called.");

            InitBehaviors();

            // save single consistent copy for building behaves since CurrentWoWContext is dynamically evaluated
            WoWContext context = CurrentWoWContext;

            // DO NOT UPDATE: This will cause a recursive event
            // Update the current context. Handled in SingularRoutine.Context.cs
            //UpdateContext();

            // special behavior - reset KitingBehavior hook prior to calling class specific createion
            TreeHooks.Instance.ReplaceHook(HookName("KitingBehavior"), new ActionAlwaysFail());

            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite(true, context, BehaviorType.Combat))
            {
                return false;
            }

            if (!EnsureComposite(true, context, BehaviorType.Pull))
            {
                return false;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            EnsureComposite(false, context, BehaviorType.Rest);
            if (TreeHooks.Instance.Hooks[HookName(BehaviorType.Rest)] == null)
                TreeHooks.Instance.ReplaceHook(HookName(BehaviorType.Rest), Helpers.Rest.CreateDefaultRestBehaviour());


            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, context, BehaviorType.CombatBuffs);
            EnsureComposite(false, context, BehaviorType.Heal);
            EnsureComposite(false, context, BehaviorType.PullBuffs);
            EnsureComposite(false, context, BehaviorType.PreCombatBuffs);

            EnsureComposite(false, context, BehaviorType.LossOfControl);

#if SHOW_BEHAVIOR_LOAD_DESCRIPTION
            // display concise single line describing what behaviors we are loading
            if (!silent)
            {
                string sMsg = "";
                if (_healBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Heal";
                if (_pullBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PullBuffs";
                if (_pullBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Pull";
                if (_preCombatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " PreCombatBuffs";
                if (_combatBuffsBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " CombatBuffs";
                if (_combatBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Combat";
                if (_restBehavior != null)
                    sMsg += (!string.IsNullOrEmpty(sMsg) ? "," : "") + " Rest";

                Logger.Write(Color.LightGreen, "Loaded{0} behaviors for {1}: {2}", Me.Specialization.ToString().CamelToSpaced(), context.ToString(), sMsg);
            }
#endif
            return true;
        }

        /// <summary>
        /// initialize all base behaviors.  replaceable portion which will vary by context is represented by a single
        /// HookExecutor that gets assigned elsewhere (typically EnsureComposite())
        /// </summary>
        private void InitBehaviors()
        {
            // be sure to turn off -- routines needing it will enable when rebuilt
            HealerManager.NeedHealTargeting = false;

            // we only do this one time
            if (_restBehavior != null)
                return;

            // note regarding behavior intros....
            // WAIT: Rest and PreCombatBuffs should wait on gcd/cast in progress (return RunStatus.Success)
            // SKIP: PullBuffs, CombatBuffs, and Heal should fall through if gcd/cast in progress (wrap in decorator)
            // HANDLE: Pull and Combat should wait or skip as needed in class specific manner required

            // loss of control behavior must be defined prior to any embedded references by other behaviors
            _lostControlBehavior = new Decorator(
                ret => HaveWeLostControl,
                new PrioritySelector(
                    new Action( r => { 
                        if ( !StyxWoW.IsInGame )
                        {
                            Logger.WriteDebug(Color.White, "Not in game...");
                            return RunStatus.Success;
                        }
                        
                        return RunStatus.Failure; 
                        }),
                    new ThrottlePasses(1, 1, new Decorator(ret => Me.Fleeing, new Action(r => { Logger.Write(Color.White, "FLEEING! (loss of control)"); return RunStatus.Failure; }))),
                    new ThrottlePasses(1, 1, new Decorator(ret => Me.Stunned, new Action(r => { Logger.Write(Color.White, "STUNNED! (loss of control)"); return RunStatus.Failure; }))),
                    new ThrottlePasses(1, 1, new Decorator(ret => Me.Silenced, new Action(r => { Logger.Write(Color.White, "SILENCED! (loss of control)"); return RunStatus.Failure; }))),
                    new Throttle(1,
                        new PrioritySelector(
                            new HookExecutor(HookName(BehaviorType.LossOfControl)),
                            new Decorator(
                                ret => SingularSettings.Instance.UseRacials,
                                new PrioritySelector(
                                    Spell.Cast("Will of the Forsaken", on => Me, ret => Me.Race == WoWRace.Undead && Me.Fleeing),
                                    Spell.Cast("Every Man for Himself", on => Me, ret => Me.Race == WoWRace.Human && (Me.Stunned || Me.Fleeing))
                                    )
                                ),

                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlled),
                            Item.UseEquippedTrinket(TrinketUsage.CrowdControlledSilenced)
                            )
                        ),
                    new ActionAlwaysSucceed()
                    )
                );

            _restBehavior = new LockSelector(
                new CallWatch( "Rest",
                    new Decorator(
                        ret => !Me.IsFlying && AllowBehaviorUsage() && !SingularSettings.Instance.DisableNonCombatBehaviors,
                        new PrioritySelector(
                            // new Action(r => { _guidLastTarget = 0; return RunStatus.Failure; }),
                            Spell.WaitForGcdOrCastOrChannel(),

                            // lost control in Rest -- force a RunStatus.Failure so we don't loop in Rest
                            new Sequence(
                                _lostControlBehavior,
                                new ActionAlwaysFail()
                                ),

                            // skip Rest logic if we lost control (since we had to return Fail to prevent Rest loop)
                            new Decorator(
                                req => !HaveWeLostControl,
                                new HookExecutor(HookName(BehaviorType.Rest))
                                )
                            )
                        )
                    )
                );

            _preCombatBuffsBehavior = new LockSelector(
                new CallWatch( "PreCombat",
                    new Decorator(  // suppress non-combat buffing if standing around waiting on DungeonBuddy or BGBuddy queues
                        ret => !Me.Mounted
                            && !SingularSettings.Instance.DisableNonCombatBehaviors
                            && AllowNonCombatBuffing(),
                        new PrioritySelector(
                            Spell.WaitForGcdOrCastOrChannel(),
                            Item.CreateUseAlchemyBuffsBehavior(),
                    // Generic.CreateFlasksBehaviour(),
                            new HookExecutor(HookName(BehaviorType.PreCombatBuffs))
                            )
                        )
                    )
                );

            _pullBuffsBehavior = new LockSelector(
                new CallWatch("PullBuffs",
                    new Decorator(
                        ret => AllowBehaviorUsage() && !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                        new HookExecutor(HookName(BehaviorType.PullBuffs))
                        )
                    )
                );

            _combatBuffsBehavior = new LockSelector(
                new CallWatch("CombatBuffs",
                    new Decorator(
                        ret => AllowBehaviorUsage() && !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                        new PrioritySelector(
                            new Decorator(ret => !HotkeyDirector.IsCombatEnabled, new ActionAlwaysSucceed()),
                            Generic.CreateUseTrinketsBehaviour(),
                            Generic.CreatePotionAndHealthstoneBehavior(),
                            Generic.CreateRacialBehaviour(),
                            new HookExecutor(HookName(BehaviorType.CombatBuffs))
                            )
                        )
                    )
                );

            _healBehavior = new LockSelector(
                new CallWatch("Heal",
                    _lostControlBehavior,
                    new Decorator(
                        ret => Kite.IsKitingActive(),
                        new HookExecutor(HookName("KitingBehavior"))
                        ),
                    new Decorator(
                        ret => AllowBehaviorUsage() && !Spell.IsGlobalCooldown() && !Spell.IsCastingOrChannelling(),
                        new HookExecutor(HookName(BehaviorType.Heal))
                        )
                    )
                );

            _pullBehavior = new LockSelector(
                new CallWatch("Pull",
                    new Decorator(
                        ret => AllowBehaviorUsage(), // && (!Me.GotTarget || !Blacklist.Contains(Me.CurrentTargetGuid, BlacklistFlags.Combat)),
                        new PrioritySelector(
                            new Decorator(
                                ret => !HotkeyDirector.IsCombatEnabled,
                                new ActionAlwaysSucceed()
                                ),
    #if BOTS_NOT_CALLING_PULLBUFFS
                            _pullBuffsBehavior,
    #endif
                            CreateLogTargetChanges("<<< PULL >>>"),
                            new HookExecutor(HookName(BehaviorType.Pull))
                            )
                        )
                    )
                );

            _combatBehavior = new LockSelector(
                new CallWatch("Combat",
                    new Decorator(
                        ret => AllowBehaviorUsage(), // && (!Me.GotTarget || !Blacklist.Contains(Me.CurrentTargetGuid, BlacklistFlags.Combat)),
                        new PrioritySelector(
                            new Decorator(
                                ret => !HotkeyDirector.IsCombatEnabled,
                                new ActionAlwaysSucceed()
                                ),
                            CreateLogTargetChanges("<<< ADD >>>"),
                            new HookExecutor(HookName(BehaviorType.Combat))
                            )
                        )
                    )
                );
        }

        private static bool HaveWeLostControl
        { 
            get
            {
                return Me.Fleeing || Me.Stunned;
            } 
        }

        internal static string HookName(string name)
        {
            return "Singular." + name;
        }

        internal static string HookName(BehaviorType typ)
        {
            return "Singular." + typ.ToString();
        }

        static bool _inQuestVehicle = false;

        static bool _inPetCombat = false;

        private static bool AllowBehaviorUsage()
        {
            // Opportunity alert -- the decision whether a Combat Routine should fight or not
            // .. should be made by the caller (BotBase, Quest Behavior, Plugin, etc.) 
            // .. The only reason for calling a Combat Routine is combat.  Anytime we have to
            // .. add this conditional check in the Combat Routine it should be a singlar that
            // .. role/responsibility boundaries are being violated

            // disable if Questing and in a Quest Vehicle (now requires setting as well)
            if (IsQuestBotActive)
            {
                if (_inQuestVehicle != Me.InVehicle)
                {
                    _inQuestVehicle = Me.InVehicle; 
                    if (_inQuestVehicle )
                    {
                        Logger.Write(Color.White, "Singular is {0} while in a Quest Vehicle", SingularSettings.Instance.DisableInQuestVehicle ? "Disabled" : "Enabled");
                        Logger.Write(Color.White, "See the [Disable in Quest Vehicle]={0} setting to change", SingularSettings.Instance.DisableInQuestVehicle);
                    }
                }

                if (_inQuestVehicle && SingularSettings.Instance.DisableInQuestVehicle)
                    return false;
            }

            // disable if in pet battle and using a plugin/botbase 
            //  ..  that doesn't prevent combat routine from being called
            //  ..  note: this won't allow pet combat to work correclty, it 
            //  ..  only prevents failed movement/spell cast messages from Singular
            //  ..  Pet Combat component to prevent calls to combat routine  as it
            //  ..  has no role in pet combat
            if (!Me.CurrentMap.IsRaid)
            {
                if (_inPetCombat != PetBattleInProgress())
                {
                    _inPetCombat = PetBattleInProgress();
                    if (_inPetCombat)
                    {
                        Logger.Write(Color.White, "Behaviors disabled in Pet Fight - contact Pet Combat bot/plugin author to fix this");
                    }
                }

                if (_inPetCombat)
                    return false;
            }

            return true;
        }

        private static bool AllowNonCombatBuffing()
        {
            // Opportunity alert -- bots that sit still waiting for a queue to pop
            // .. should avoid calling PreCombatbuff, since it looks odd for long queue times
            // .. for a toon to stay stationary but renew a buff immediately as it expires.

            if (IsBgBotActive && !Battlegrounds.IsInsideBattleground)
                return false;

            if (IsDungeonBuddyActive && !Me.IsInInstance)
                return false;

            if (!AllowBehaviorUsage())
                return false;

            return true;
        }

        private static bool PetBattleInProgress()
        {
            try
            {
                return 1 == Lua.GetReturnVal<int>("return C_PetBattles.IsInBattle()", 0);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures we have a composite for the given BehaviorType.  
        /// </summary>
        /// <param name="error">true: report error if composite not found, false: allow null composite</param>
        /// <param name="type">BehaviorType that should be loaded</param>
        /// <returns>true: composite loaded and saved to hook, false: failure</returns>
        private bool EnsureComposite(bool error, WoWContext context, BehaviorType type)
        {
            int count = 0;
            Composite composite;

            Logger.WriteDebug("Creating " + type + " behavior.");

            composite = CompositeBuilder.GetComposite(Class, TalentManager.CurrentSpec, type, context, out count);

            // handle those composites we need to default if not found
            if (composite == null)
            {
                if (type == BehaviorType.Rest)
                    composite = Helpers.Rest.CreateDefaultRestBehaviour();
            }

            TreeHooks.Instance.ReplaceHook(HookName(type), composite);

            if ((composite == null || count <= 0) && error)
            {
                StopBot(string.Format("Singular does not support {0} for this {1} {2} in {3} context!", type, StyxWoW.Me.Class, TalentManager.CurrentSpec, context));
                return false;
            }

            return composite != null;
        }

        private static Composite CreateLogTargetChanges(string sType)
        {
            return new Action(r =>
                {
                    // there are moments where CurrentTargetGuid != 0 but CurrentTarget == null. following
                    // .. tries to handle by only checking CurrentTarget reference and treating null as guid = 0
                    if ((SingularSettings.Debug && Me.CurrentTargetGuid != _guidLastTarget))
                    {
                        if (Me.CurrentTarget == null)
                        {
                            if (_guidLastTarget != 0)
                            {
                                Logger.WriteDebug(sType + " CurrentTarget now: (null)");
                                _guidLastTarget = 0;
                            }
                        }
                        else
                        {
                            _guidLastTarget = Me.CurrentTargetGuid;

                            string info = "";
                            WoWUnit target = Me.CurrentTarget;

                            if (Styx.CommonBot.POI.BotPoi.Current.Guid == Me.CurrentTargetGuid)
                                info += string.Format(", IsBotPoi={0}", Styx.CommonBot.POI.BotPoi.Current.Type);

                            if (Styx.CommonBot.Targeting.Instance.TargetList.Contains(Me.CurrentTarget))
                                info += string.Format(", TargetIndex={0}", Styx.CommonBot.Targeting.Instance.TargetList.IndexOf(Me.CurrentTarget) + 1);

                            Logger.WriteDebug(sType + " CurrentTarget now: {0} h={1:F1}%, maxh={2}, d={3:F1} yds, box={4:F1}, player={5}, hostile={6}, faction={7}, loss={8}, facing={9}" + info,
                                target.SafeName(),
                                target.HealthPercent,
                                target.MaxHealth,
                                target.Distance,
                                target.CombatReach,
                                target.IsPlayer.ToYN(),
                                target.IsHostile.ToYN(),
                                target.FactionId ,
                                target.InLineOfSpellSight.ToYN(),
                                Me.IsSafelyFacing(target).ToYN()
                                );
                        }

                        _timerLastTarget.Reset();
                    }

                    return RunStatus.Failure;
                });

        }

        private static int _prevPullDistance = -1;

        private static void MonitorPullDistance()
        {
            if (_prevPullDistance != CharacterSettings.Instance.PullDistance)
            {
                _prevPullDistance = CharacterSettings.Instance.PullDistance;
                Logger.Write(Color.White, "attention: Pull Distance set to {0} yds by {1}, Plug-in, Profile, or User", _prevPullDistance, GetBotName());
            }
        }

        #region Nested type: LockSelector

        /// <summary>
        /// This behavior wraps the child behaviors in a 'FrameLock' which can provide a big performance improvement 
        /// if the child behaviors makes multiple api calls that internally run off a frame in WoW in one CC pulse.
        /// </summary>
        private class LockSelector : PrioritySelector
        {
            delegate RunStatus TickDelegate(object context);

            TickDelegate _TickSelectedByUser;

            public LockSelector(params Composite[] children)
                : base(children)
            {
                if (SingularSettings.Instance.UseFrameLock)
                    _TickSelectedByUser = TickWithFrameLock;
                else
                    _TickSelectedByUser = TickNoFrameLock;
            }

            public override RunStatus Tick(object context)
            {
                return _TickSelectedByUser(context);
            }

            private RunStatus TickWithFrameLock(object context)
            {
                using (StyxWoW.Memory.AcquireFrame())
                {
                    return base.Tick(context);
                }
            }

            private RunStatus TickNoFrameLock(object context)
            {
                return base.Tick(context);
            }

        }

        #endregion
    }


    public class CallWatch : PrioritySelector
    {
        public static DateTime LastCall { get; set; }
        public static ulong CountCall { get; set; }
        public static double WarnTime { get; set; }
        public static TimeSpan SinceLast
        {
            get
            {
                TimeSpan since;
                if (LastCall == DateTime.MinValue)
                    since = TimeSpan.Zero;
                else
                    since = DateTime.Now - LastCall;
                return since;
            }
        }

        public string Name { get; set; }

        private static bool _init = false;

        private static void Initialize()
        {
            if (_init)
                return;

            _init = true;
            LastCall = DateTime.MinValue;

            SingularRoutine.OnBotEvent += (src, arg) =>
            {
                // reset time on Start
                if (arg.Event == SingularBotEvent.BotStart)
                    LastCall = DateTime.Now;
                else if (arg.Event == SingularBotEvent.BotStop)
                {
                    TimeSpan since = SinceLast;
                    if (since.TotalSeconds >= WarnTime)
                    {
                        if (SingularSettings.Debug)
                            Logger.WriteDebug(Color.HotPink, "warning: {0:F1} seconds since BotBase last called Singular (now in OnBotStop)", since.TotalSeconds);
                        else
                            Logger.WriteFile("warning: {0:F1} seconds since BotBase last called Singular (now in OnBotStop)", since.TotalSeconds);
                    }
                }
            };
        }

        public CallWatch(string name, params Composite[] children)
            : base(children)
        {
            Initialize();

            if (WarnTime == 0)
                WarnTime = 5;

            Name = name;
        }
        /*
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            IEnumerable<RunStatus> ret;
            CountCall++;

            if (SingularSettings.Debug)
            {
                if ((DateTime.Now - LastCall).TotalSeconds > WarnTime && LastCall != DateTime.MinValue)
                    Logger.WriteDebug(Color.HotPink, "warning: {0:F1} seconds since BotBase last called Singular (now in {1})", (DateTime.Now - LastCall).TotalSeconds, Name);
            }

            if (!CallTrace)
            {
                ret = base.Execute(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.Write(Color.DodgerBlue, "enter: {0}", Name);
                ret = base.Execute(context);
                Logger.Write(Color.DodgerBlue, "leave: {0}, took {1} ms", Name, (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            LastCall = DateTime.Now;
            return ret;
        }
        */
        public override RunStatus Tick(object context)
        {
            RunStatus ret;
            CountCall++;

            if (SingularSettings.Debug)
            {
                TimeSpan since = SinceLast;
                if (since.TotalSeconds > WarnTime && LastCall != DateTime.MinValue)
                    Logger.WriteDebug(Color.HotPink, "warning: {0:F1} seconds since BotBase last called Singular (now in {1})", since.TotalSeconds, Name);
            }

            if (!SingularSettings.Trace )
            {
                ret = base.Tick(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.WriteDebug(Color.DodgerBlue, "enter: {0}", Name);
                ret = base.Tick(context);
                Logger.WriteDebug(Color.DodgerBlue, "leave: {0}, took {1} ms", Name, (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            LastCall = DateTime.Now;
            return ret;
        }

    }

    public class CallTrace : PrioritySelector
    {
        public static DateTime LastCall { get; set; }
        public static ulong CountCall { get; set; }
        public static bool TraceActive { get { return SingularSettings.Trace; } }

        public string Name { get; set; }

        private static bool _init = false;

        private static void Initialize()
        {
            if (_init)
                return;

            _init = true;
        }

        public CallTrace(string name, params Composite[] children)
            : base(children)
        {
            Initialize();

            Name = name;
            LastCall = DateTime.MinValue;
        }

        public override RunStatus Tick(object context)
        {
            RunStatus ret;
            CountCall++;

            if (!TraceActive )
            {
                ret = base.Tick(context);
            }
            else
            {
                DateTime started = DateTime.Now;
                Logger.WriteDebug(Color.LightBlue, "... enter: {0}", Name);
                ret = base.Tick(context);
                Logger.WriteDebug(Color.LightBlue, "... leave: {0}, took {1} ms", Name, (ulong)(DateTime.Now - started).TotalMilliseconds);
            }

            return ret;
        }

    }
}