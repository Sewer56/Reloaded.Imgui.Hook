using System;
using System.Diagnostics;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
using SharpDX.Direct3D9;
using IDirect3DDevice9 = DearImguiSharp.IDirect3DDevice9;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookDX9 : IImguiHook
    {
        public ImguiHook ImguiHook { get; private set; }
        public DX9Hook Hook { get; private set; }

        private IHook<DX9Hook.EndScene> _endSceneHook;
        private IHook<DX9Hook.Reset> _resetHook;
        private IHook<DX9Hook.CreateDevice> _createDeviceHook;
        private bool _initialized = false;
        private IntPtr _windowHandle;

        public ImguiHookDX9(ImguiHook hook)
        {
            ImguiHook = hook;
            Hook = new DX9Hook(SDK.Hooks);
            _endSceneHook = Hook.DeviceVTable.CreateFunctionHook<DX9Hook.EndScene>((int) DirectX.Definitions.IDirect3DDevice9.EndScene, EndScene).Activate();
            _resetHook = Hook.DeviceVTable.CreateFunctionHook<DX9Hook.Reset>((int)DirectX.Definitions.IDirect3DDevice9.Reset, Reset).Activate();
            _createDeviceHook = Hook.Direct3D9VTable.CreateFunctionHook<DX9Hook.CreateDevice>((int) IDirect3D9.CreateDevice, CreateDevice).Activate();
        }

        ~ImguiHookDX9()
        {
            ReleaseUnmanagedResources();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            ImGui.ImGuiImplDX9Shutdown();
        }

        private unsafe IntPtr CreateDevice(IntPtr direct3dpointer, uint adapter, DeviceType devicetype, IntPtr hfocuswindow, CreateFlags behaviorflags, ref PresentParameters ppresentationparameters, int** ppreturneddeviceinterface)
        {
            if (hfocuswindow != IntPtr.Zero)
                _windowHandle = hfocuswindow;
            else if (ppresentationparameters.DeviceWindowHandle != IntPtr.Zero)
                _windowHandle = ppresentationparameters.DeviceWindowHandle;

            Misc.Debug.WriteLine($"Create Window Handle {(long)_windowHandle:X}");
            return _createDeviceHook.OriginalFunction(direct3dpointer, adapter, devicetype, hfocuswindow, behaviorflags, ref ppresentationparameters, ppreturneddeviceinterface);
        }


        private unsafe IntPtr EndScene(IntPtr device)
        {
            if (!_initialized)
            {
                // Try our best to initialize if not hooked at boot.
                // This can fail though if window handle is only passed in presentation parameters.
                if (_windowHandle == IntPtr.Zero)
                {
                    var dev = new Device(device);
                    _windowHandle = dev.CreationParameters.HFocusWindow;
                }

                Misc.Debug.WriteLine($"EndScene Window Handle {(long)_windowHandle:X}");
                if (_windowHandle == IntPtr.Zero)
                    return _endSceneHook.OriginalFunction(device);

                ImGui.ImGuiImplDX9Init((void*)device);
                _initialized = true;
            }

            ImGui.ImGuiImplDX9NewFrame();
            ImguiHook.NewFrame();
            using var drawData = ImGui.GetDrawData();
            ImGui.ImGuiImplDX9RenderDrawData(drawData);

            return _endSceneHook.OriginalFunction(device);
        }

        private IntPtr Reset(IntPtr device, ref PresentParameters presentParameters)
        {
            Misc.Debug.WriteLine($"Reset Handle {(long)presentParameters.DeviceWindowHandle:X}");
            ImGui.ImGuiImplDX9InvalidateDeviceObjects();
            var result = _resetHook.OriginalFunction(device, ref presentParameters);
            ImGui.ImGuiImplDX9CreateDeviceObjects();
            return result;
        }

        public void Disable()
        {
            _endSceneHook.Disable();
            _resetHook.Disable();
        }

        public void Enable()
        {
            _endSceneHook.Enable();
            _resetHook.Enable();
        }

        public IntPtr GetWindowHandle() => _windowHandle;
    }
}
