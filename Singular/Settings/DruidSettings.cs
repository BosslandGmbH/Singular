namespace Singular.Settings
{
    class DruidSettings : Styx.Helpers.Settings
    {
        public DruidSettings()
            : base(SingularSettings.SettingsPath + "_Druid.xml")
        {
        }
    }
}