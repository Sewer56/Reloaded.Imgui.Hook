using System;

namespace Reloaded.Imgui.Hook.Implementations
{
    public interface IImguiHook : IDisposable
    {
        /// <summary>
        /// True if the API is supported for the current process.
        /// </summary>
        bool IsApiSupported();

        /// <summary>
        /// Initializes the hooks specific to this graphics API.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Disables the hooks used by this implementation.
        /// </summary>
        void Disable();

        /// <summary>
        /// Re-enables the hooks used by this implementation.
        /// </summary>
        void Enable();
    }
}
