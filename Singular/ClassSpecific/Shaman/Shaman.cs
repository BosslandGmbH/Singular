#region Revision Info

// This file is part of Singular - A community driven Honorbuddy CC
// $Author: $
// $Date: $
// $HeadURL: $
// $LastChangedBy: $
// $LastChangedDate: $
// $LastChangedRevision:  $
// $Revision:  $

#endregion

using System.Collections.Generic;
using System.Linq;

using Singular.Settings;

using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Logic;
using Styx.Logic.Combat;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using System;

namespace Singular
{
    partial class SingularRoutine
    {
        /// <summary>
        /// WillChainHealHop()
        /// Tests whether casting Chain Lightning on 'healTarget' results in a minimum 
        /// of 2 hops (3 people healed.) 
        /// </summary>
        /// <param name="healTarget"></param>
        /// <returns></returns>
        private bool WillChainHealHop(WoWUnit healTarget)
        {
            double threshhold = SingularSettings.Instance.Shaman.RAF_ChainHeal_Health;

            if (healTarget == null)
                return false;

            var t = (from o in ObjectManager.ObjectList
                     where o is WoWPlayer && healTarget.Location.Distance(o.Location) < 12
                     let p = o.ToPlayer()
                     where p != null
                           && p.IsHorde == Me.IsHorde
                           && !p.IsPet
                           && p != healTarget
                           && p.IsAlive
                           && p.HealthPercent < threshhold
                     let c = (from oo in ObjectManager.ObjectList
                              where oo is WoWPlayer && p.Location.Distance(oo.Location) < 12
                              let pp = oo.ToPlayer()
                              where pp != null
                                    && pp.IsHorde == p.IsHorde
                                    && !pp.IsPet
                                    && pp.IsAlive
                                    && pp.HealthPercent < threshhold
                              select pp).Count()
                     orderby c descending, p.Distance ascending
                     select new { Player = p, Count = c }).FirstOrDefault();

            if (t == null || t.Count < 3)
                return false;
            return true;
        }

        /// <summary>
        /// WillChainHealHop()
        /// Tests whether casting Chain Lightning on 'healTarget' results in a minimum 
        /// of 2 hops (3 people healed.) 
        /// </summary>
        /// <param name="healTarget"></param>
        /// <returns></returns>
        private bool WillChainLightningHop()
        {
            if (Me.GotTarget && Me.CurrentTarget.IsPlayer && Me.IsHorde != ((WoWPlayer)Me.CurrentTarget).IsHorde )
            {
                return  (from o in ObjectManager.ObjectList
                         where o is WoWPlayer 
                            && o != Me.CurrentTarget
                            && Me.CurrentTarget.Location.Distance(o.Location) < 12
                         let p = o.ToPlayer()
                         where p != null
                            && p.IsHorde != Me.IsHorde
                            && !p.IsPet
                            && p.IsAlive
                         select p).Any();
            }

            return false;
        }
    }
}