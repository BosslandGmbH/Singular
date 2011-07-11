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
using System.Reflection;
using Singular.Dynamics;
using Singular.GUI;
using Singular.Managers;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace Singular
{
    public partial class SingularRoutine : CombatRoutine
    {
        private Composite _combatBehavior;
        private Composite _combatBuffsBehavior;
        private Composite _healBehavior;
        private WoWClass _myClass;
        private Composite _preCombatBuffsBehavior;
        private Composite _pullBehavior;
        private Composite _pullBuffsBehavior;
        private Composite _restBehavior;

        public SingularRoutine()
        {
            Instance = this;
        }

        public static SingularRoutine Instance { get; private set; }

        public override string Name { get { return "Singular v2 $Revision$"; } }

        public override WoWClass Class { get { return StyxWoW.Me.Class; } }

        public override bool WantButton { get { return true; } }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        internal static WoWClass MyClass { get; set; }

        internal static WoWContext LastWoWContext { get; set; }

        internal static WoWContext CurrentWoWContext
        {
            get
            {
                if (Battlegrounds.IsInsideBattleground)
                {
                    return WoWContext.Battlegrounds;
                }
                if (StyxWoW.Me.IsInInstance)
                {
                    return WoWContext.Instances;
                }
                return WoWContext.Normal;
            }
        }

        public override Composite CombatBehavior { get { return _combatBehavior; } }

        public override Composite CombatBuffBehavior { get { return _combatBuffsBehavior; } }

        public override Composite HealBehavior { get { return _healBehavior; } }

        public override Composite PreCombatBuffBehavior { get { return _preCombatBuffsBehavior; } }

        public override Composite PullBehavior { get { return _pullBehavior; } }

        public override Composite PullBuffBehavior { get { return _pullBuffsBehavior; } }

        public override Composite RestBehavior { get { return _restBehavior; } }

        private static bool IsMounted
        {
            get
            {
                switch (StyxWoW.Me.Shapeshift)
                {
                    case ShapeshiftForm.FlightForm:
                    case ShapeshiftForm.EpicFlightForm:
                        return true;
                }
                return StyxWoW.Me.Mounted;
            }
        }

        public override void OnButtonPress()
        {
            new ConfigurationForm().ShowDialog();
        }

        public override void Pulse()
        {
            PetManager.Pulse();

            if (HealerManager.NeedHealTargeting)
                HealerManager.Instance.Pulse();

            if (TankManager.NeedTankTargeting && CurrentWoWContext != WoWContext.Battlegrounds && (Me.IsInParty || Me.IsInRaid))
                TankManager.Instance.Pulse();
        }

        public override void Initialize()
        {
            //Caching current class here to avoid issues with loading screens where Class return None and we cant build behaviors
            _myClass = Me.Class;

            Logger.Write("Starting Singular v" + Assembly.GetExecutingAssembly().GetName().Version);
            Logger.Write("Determining talent spec.");
            try
            {
                TalentManager.Update();
            }
            catch (Exception e)
            {
                StopBot(e.ToString());
            }
            Logger.Write("Current spec is " + TalentManager.CurrentSpec.ToString().CamelToSpaced());

            if (!CreateBehaviors())
            {
                return;
            }
            Logger.Write("Behaviors created!");
        }

        public bool CreateBehaviors()
        {
            //Caching the context to not recreate same behaviors repeatedly.
            LastWoWContext = CurrentWoWContext;

            // If these fail, then the bot will be stopped. We want to make sure combat/pull ARE implemented for each class.
            if (!EnsureComposite(true, BehaviorType.Combat, out _combatBehavior))
            {
                return false;
            }

            if (!EnsureComposite(true, BehaviorType.Pull, out _pullBehavior))
            {
                return false;
            }

            // If there's no class-specific resting, just use the default, which just eats/drinks when low.
            if (!EnsureComposite(false, BehaviorType.Rest, out _restBehavior))
            {
                Logger.Write("Using default rest behavior.");
                _restBehavior = Helpers.Rest.CreateDefaultRestBehaviour();
            }

            // These are optional. If they're not implemented, we shouldn't stop because of it.
            EnsureComposite(false, BehaviorType.CombatBuffs, out _combatBuffsBehavior);
            EnsureComposite(false, BehaviorType.Heal, out _healBehavior);
            EnsureComposite(false, BehaviorType.PullBuffs, out _pullBuffsBehavior);
            EnsureComposite(false, BehaviorType.PreCombatBuffs, out _preCombatBuffsBehavior);

            // Since we can be lazy, we're going to fix a bug right here and now.
            // We should *never* cast buffs while mounted. EVER. So we simply wrap it in a decorator, and be done with it.
            if (_preCombatBuffsBehavior != null)
            {
                _preCombatBuffsBehavior = new Decorator(
                    ret => !IsMounted && !Me.IsOnTransport, new PrioritySelector(
                        _preCombatBuffsBehavior));
            }
            if (_combatBuffsBehavior != null)
            {
                _combatBuffsBehavior = new Decorator(
                    ret => !IsMounted && !Me.IsOnTransport,
                    new PrioritySelector(
                        _combatBuffsBehavior)
                    );
            }

            // There are some classes that uses spells in rest behavior. Basicly we don't want Rest to be called while flying.
            if (_restBehavior != null)
            {
                _restBehavior = new Decorator(
                    ret => !Me.IsFlying,
                    new PrioritySelector(
                        _restBehavior));
            }

            return true;
        }

        private bool EnsureComposite(bool error, BehaviorType type, out Composite composite)
        {
            Logger.WriteDebug("Creating " + type + " behavior.");
            int count = 0;
            composite = CompositeBuilder.GetComposite(_myClass, TalentManager.CurrentSpec, type, CurrentWoWContext, out count);
            if ((composite == null || count <= 0) && error)
            {
                StopBot(
                    string.Format(
                        "Singular currently does not support {0} for this class/spec combination, in this context! [{1}, {2}, {3}]", type, _myClass,
                        TalentManager.CurrentSpec, CurrentWoWContext));
                return false;
            }
            return composite != null;
        }

        private static void StopBot(string reason)
        {
            Logger.Write(reason);
            TreeRoot.Stop();
        }
    }
}