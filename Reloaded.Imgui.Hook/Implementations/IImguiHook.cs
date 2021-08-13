using System;

namespace Reloaded.Imgui.Hook.Implementations
{
    public interface IImguiHook : IDisposable
    {
        void Disable();
        void Enable();
    }
}
