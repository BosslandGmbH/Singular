
using System;
using Styx.CommonBot.Routines;

namespace Singular
{
    /// <summary>
    /// Template File:  SingularRoutine.Version.tmpl
    /// Generated File: SingularRoutine.Version.cs
    /// 
    /// These files are the source and output for SubWCRev.exe included
    /// with TortoiseSVN.  The purpose is to provide a real Build #
    /// automatically updated with each release.
    /// 
    /// To make changes, be sure to edit SingularRoutine.Version.tmpl
    /// as the .cs version gets overwritten each build
    /// 
    /// Singular SVN Information
    /// -------------------------
    /// Revision 1319
    /// Date     2012/12/02 23:29:46
    /// Range    1318:1319
    /// 
    /// </summary>
    public partial class SingularRoutine : CombatRoutine
    {
        // HB Build Process is overwriting AssemblyInfo.cs contents,
        // ... so manage version here instead of reading assembly
        // --------------------------------------------------------

        // return Assembly.GetExecutingAssembly().GetName().Version;
        public static Version GetSingularVersion()
        {
            return new Version("3.0.0.1319");
        }
    }
}