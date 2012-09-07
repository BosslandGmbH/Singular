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

using System.IO;

namespace Singular.Settings
{
    internal class WarlockSettings : Styx.Helpers.Settings
    {
        public WarlockSettings()
            : base(Path.Combine(SingularSettings.SettingsPath, "Warlock.xml"))
        {
        }
    }
}