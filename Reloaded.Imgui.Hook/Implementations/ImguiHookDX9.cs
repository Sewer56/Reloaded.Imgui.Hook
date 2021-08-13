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
using Debug = Reloaded.Imgui.Hook.Misc.Debug;
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
        private bool _endSceneRecursionLock = false;

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
            var windowHandle = hfocuswindow != IntPtr.Zero ? hfocuswindow : ppresentationparameters->DeviceWindowHandle;

            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(windowHandle))
            {
                Debug.WriteLine($"[DX9 EndScene] Discarding Window Handle");
                return _createDeviceHook.OriginalFunction.Value.Invoke(direct3dpointer, adapter, devicetype, hfocuswindow, behaviorflags, ppresentationparameters, ppreturneddeviceinterface);
            }

            if (windowHandle != IntPtr.Zero)
                _windowHandle = hfocuswindow;

            Debug.WriteLine($"Create Window Handle {(long)_windowHandle:X}");
            return _createDeviceHook.OriginalFunction.Value.Invoke(direct3dpointer, adapter, devicetype, hfocuswindow, behaviorflags, ppresentationparameters, ppreturneddeviceinterface);
        }

        private unsafe IntPtr EndSceneImpl(IntPtr device)
        {
            // With multi-viewports, ImGui might call EndScene again; so we need to prevent stack overflow here.
            if (_endSceneRecursionLock)
                return _endSceneHook.OriginalFunction.Value.Invoke(device);

            _endSceneRecursionLock = true;
            var dev = new Device(device);
            var windowHandle = dev.CreationParameters.HFocusWindow;

            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(windowHandle))
            {
                Debug.WriteLine($"[DX9 EndScene] Discarding Window Handle");
                return _endSceneHook.OriginalFunction.Value.Invoke(device);
            }

            if (!_initialized)
            {
                // Try our best to initialize if not hooked at boot.
                // This can fail though if window handle is only passed in presentation parameters.
                if (_windowHandle == IntPtr.Zero)
                    _windowHandle = windowHandle;

                Debug.WriteLine($"EndScene Window Handle {(long)_windowHandle:X}");
                if (_windowHandle == IntPtr.Zero)
                    return _endSceneHook.OriginalFunction.Value.Invoke(device);

                ImGui.ImGuiImplDX9Init((void*)device);
                _initialized = true;
            }

            ImGui.ImGuiImplDX9NewFrame();
            ImguiHook.NewFrame(_windowHandle);
            using var drawData = ImGui.GetDrawData();
            ImGui.ImGuiImplDX9RenderDrawData(drawData);

            _endSceneRecursionLock = false;
            return _endSceneHook.OriginalFunction.Value.Invoke(device);
        }

        private IntPtr ResetImpl(IntPtr device, PresentParameters* presentParameters)
        {
            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(presentParameters->DeviceWindowHandle))
            {
                Debug.WriteLine($"[DX9 EndScene] Discarding Window Handle");
                return _endSceneHook.OriginalFunction.Value.Invoke(device);
            }

            Debug.WriteLine($"Reset Handle {(long)presentParameters->DeviceWindowHandle:X}");
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
