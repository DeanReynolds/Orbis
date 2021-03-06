﻿using System;

namespace Orbis
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Settings.HandleArgs(args);
            using (var game = new Game())
                game.Run();
        }
    }
#endif
}