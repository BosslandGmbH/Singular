using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Styx;
using Styx.Helpers;

namespace Singular
{
    partial class SingularRoutine
    {
        public void AttachEventHandlers()
        {
            BotEvents.Player.OnMapChanged += Player_OnMapChanged;
        }

        void Player_OnMapChanged(BotEvents.Player.MapChangedEventArgs args)
        {
            Logger.Write("Map changed. New context: " + CurrentWoWContext + ". Rebuilding behaviors.");
            CreateBehaviors();
        }
    }
}
