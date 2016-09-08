using SharpXNA.Plugins;

namespace Orbis
{
    public static class Settings
    {

        /*
               Reconfigured/tweaked to fix all bugs, if you're concerned about any of this
               refer to https://github.com/DeanReynolds/C--INI-Parser-Writer/blob/master/INI.cs
               that's the ini parser/reader I wrote and use in my engine
        */

        private static INI ini;
        
        static Settings() { ini = INI.ReadFile("settings.ini"); }

        public static string Get(string key) { return ini.Get(key); }
        public static string Get(string section, string key) { return ini.Get(section, key); }
        public static void Set(string key, string value) { ini.Set(key, value, true); }
        public static void Set(string section, string key, string value) { ini.Set(section, key, value, true); }
    }
}