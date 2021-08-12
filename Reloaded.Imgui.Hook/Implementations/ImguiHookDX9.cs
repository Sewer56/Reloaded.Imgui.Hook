using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
using Reloaded.Imgui.Hook.Misc;
using SharpDX.Direct3D9;
using Format = SharpDX.DXGI.Format;
using IDirect3DDevice9 = DearImguiSharp.IDirect3DDevice9;
using PresentFlags = SharpDX.DXGI.PresentFlags;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookDX9 : IImguiHook
    {
        public static ImguiHookDX9 Instance { get; private set; } = new ImguiHookDX9();

        private IHook<DX9Hook.EndScene> _endSceneHook;
        private IHook<DX9Hook.Reset> _resetHook;
        private IHook<DX9Hook.CreateDevice> _createDeviceHook;
        private bool _initialized = false;
        private IntPtr _windowHandle;

        public ImguiHookDX9()
        {
            var endScenePtr     = (long) DX9Hook.DeviceVTable[(int) DirectX.Definitions.IDirect3DDevice9.EndScene].FunctionPointer;
            var resetPtr        = (long) DX9Hook.DeviceVTable[(int)DirectX.Definitions.IDirect3DDevice9.Reset].FunctionPointer;
            var createDevicePtr = (long) DX9Hook.DeviceVTable[(int)IDirect3D9.CreateDevice].FunctionPointer;

            _endSceneHook = SDK.Hooks.CreateHook<DX9Hook.EndScene>(typeof(ImguiHookDX9), nameof(EndSceneImplStatic), endScenePtr).Activate();
            _resetHook = SDK.Hooks.CreateHook<DX9Hook.Reset>(typeof(ImguiHookDX9), nameof(ResetImplStatic), resetPtr).Activate();
            _createDeviceHook = SDK.Hooks.CreateHook<DX9Hook.CreateDevice>(typeof(ImguiHookDX9), nameof(CreateDeviceImplStatic), createDevicePtr).Activate();
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
            if (_initialized)
                ImGui.ImGuiImplDX9Shutdown();
        }

        private unsafe IntPtr CreateDeviceImpl(IntPtr direct3dpointer, uint adapter, DeviceType devicetype, IntPtr hfocuswindow, CreateFlags behaviorflags, PresentParameters* ppresentationparameters, int** ppreturneddeviceinterface)
        {
            if (hfocuswindow != IntPtr.Zero)
                _windowHandle = hfocuswindow;
            else if (ppresentationparameters->DeviceWindowHandle != IntPtr.Zero)
                _windowHandle = ppresentationparameters->DeviceWindowHandle;

            Misc.Debug.WriteLine($"Create Window Handle {(long)_windowHandle:X}");
            return _createDeviceHook.OriginalFunction.Value.Invoke(direct3dpointer, adapter, devicetype, hfocuswindow, behaviorflags, ppresentationparameters, ppreturneddeviceinterface);
        }


        private unsafe IntPtr EndSceneImpl(IntPtr device)
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
                    return _endSceneHook.OriginalFunction.Value.Invoke(device);

                ImGui.ImGuiImplDX9Init((void*)device);
                _initialized = true;
            }

            ImGui.ImGuiImplDX9NewFrame();
            ImguiHook.NewFrame();
            using var drawData = ImGui.GetDrawData();
            ImGui.ImGuiImplDX9RenderDrawData(drawData);

            return _endSceneHook.OriginalFunction.Value.Invoke(device);
        }

        private IntPtr ResetImpl(IntPtr device, PresentParameters* presentParameters)
        {
            Misc.Debug.WriteLine($"Reset Handle {(long)presentParameters->DeviceWindowHandle:X}");
            ImGui.ImGuiImplDX9InvalidateDeviceObjects();
            var result = _resetHook.OriginalFunction.Value.Invoke(device, presentParameters);
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

        #region Hook Functions
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr CreateDeviceImplStatic(IntPtr direct3dpointer, uint adapter, DeviceType devicetype, IntPtr hfocuswindow, CreateFlags behaviorflags, PresentParameters* ppresentationparameters, int** ppreturneddeviceinterface) => Instance.CreateDeviceImpl(direct3dpointer, adapter, devicetype, hfocuswindow, behaviorflags, ppresentationparameters, ppreturneddeviceinterface);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr EndSceneImplStatic(IntPtr device) => Instance.EndSceneImpl(device);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr ResetImplStatic(IntPtr device, PresentParameters* presentParameters) => Instance.ResetImpl(device, presentParameters);
        #endregion
    }
}
