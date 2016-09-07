using System;
using System.Collections.Generic;
using System.Linq;
using SharpXNA.Plugins;

namespace Orbis
{
    public static class Settings
    {
        private static INI ini;
        private static readonly Dictionary<string, string> Config = new Dictionary<string, string>();

        public static void Parse()
        {
            ini = INI.ReadFile("settings.ini");
            // Copy all the keys and values over.
            for (var i = 0; i < ini.Nodes.Count; i++)
            {
                Config.Add(ini.Nodes.Keys.OfType<string>().ToArray()[i], ini.Nodes.Values.OfType<string>().ToArray()[i]);
            }
        }

        public static string Get(string key)
        {
            string value;
            if (Config.TryGetValue(key, out value))
            {
                return value;
            }
            throw new ArgumentException();
        }

        public static void Set(string key, string value)
        {
            // If the key exists...
            string checkValue;
            if (Config.TryGetValue(key, out checkValue))
            {
                // Set it.
                ini.Set(key, value);
                Console.WriteLine("Changed setting '" + key + "' from '" + checkValue + "' to '" + value + "'.");
                ini.Save("settings.ini");
            }
            else
            {
                // If not, throw a tantrum/exception.
                throw new ArgumentException();
            }
        }
    }
}
