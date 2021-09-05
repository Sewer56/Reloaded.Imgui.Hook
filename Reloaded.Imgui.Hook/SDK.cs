using System;
using System.Collections.Generic;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.Implementations;

namespace Reloaded.Imgui.Hook
{
    public static class SDK
    {
        /// <summary>
        /// Instance of the Reloaded.Hooks library.
        /// </summary>
        public static IReloadedHooks Hooks { get; private set; }

        /// <summary>
        /// Allows you to receive debugging notifications.
        /// </summary>
        public static Action<string> Debug { get; set; }
        
        /// <summary>
        /// Initializes the library.
        /// </summary>
        /// <param name="_hooks">Instance of the Reloaded.Hooks library.</param>
        /// <param name="_debug">Specifies a method to receive debugging printouts.</param>
        public static void Init(IReloadedHooks _hooks, Action<string> _debug = null)
        {
            Hooks = _hooks;
            Debug = _debug;
        }
    }
}
