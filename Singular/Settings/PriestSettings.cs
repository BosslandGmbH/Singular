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




namespace Singular.Settings
{
    internal class PriestSettings : Styx.Helpers.Settings
    {
        public PriestSettings()
            : base(SingularSettings.SettingsPath + "_Priest.xml")
        {
        }
    }
}