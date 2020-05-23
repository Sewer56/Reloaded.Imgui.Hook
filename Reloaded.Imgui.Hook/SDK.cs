using Reloaded.Hooks.Definitions;

namespace Reloaded.Imgui.Hook
{
    public static class SDK
    {
        /// <summary>
        /// Instance of the Reloaded.Hooks library.
        /// </summary>
        public static IReloadedHooks Hooks { get; private set; }

        /// <summary>
        /// Initializes the library.
        /// </summary>
        /// <param name="_hooks">Instance of the Reloaded.Hooks library.</param>
        public static void Init(IReloadedHooks _hooks)
        {
            Hooks = _hooks;
        }
    }
}
