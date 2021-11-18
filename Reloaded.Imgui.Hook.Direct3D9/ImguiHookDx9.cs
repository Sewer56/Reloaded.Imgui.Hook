using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.Direct3D9;
using SharpDX.Direct3D9;
using static Reloaded.Imgui.Hook.Misc.Native;
using Debug = Reloaded.Imgui.Hook.Misc.Debug;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookDx9 : IImguiHook
    {
        public static ImguiHookDx9 Instance { get; private set; }

        private IHook<DX9Hook.Release> _releaseHook;
        private IHook<DX9Hook.EndScene> _endSceneHook;
        private IHook<DX9Hook.Reset> _resetHook;
        private bool _initialized = false;
        private IntPtr _windowHandle;
        private IntPtr _device;

        /*
         * In some cases (E.g. under DX9 + Viewports enabled), Dear ImGui might call
         * DirectX functions from within its internal logic.
         *
         * We put a lock on the current thread in order to prevent stack overflow.
         */

        private bool _releaseRecursionLock = false;
        private bool _endSceneRecursionLock = false;
        private bool _resetRecursionLock = false;

        public ImguiHookDx9() { }

        public void Initialize()
        {
            var releasePtr = (long)DX9Hook.DeviceVTable[(int)Direct3D9.Definitions.IDirect3DDevice9.Release].FunctionPointer;
            var endScenePtr = (long)DX9Hook.DeviceVTable[(int)Direct3D9.Definitions.IDirect3DDevice9.EndScene].FunctionPointer;
            var resetPtr = (long)DX9Hook.DeviceVTable[(int)Direct3D9.Definitions.IDirect3DDevice9.Reset].FunctionPointer;

            Instance = this;
            _releaseHook = SDK.Hooks.CreateHook<DX9Hook.Release>(typeof(ImguiHookDx9), nameof(ReleaseStatic), releasePtr).Activate();
            _endSceneHook = SDK.Hooks.CreateHook<DX9Hook.EndScene>(typeof(ImguiHookDx9), nameof(EndSceneImplStatic), endScenePtr).Activate();
            _resetHook = SDK.Hooks.CreateHook<DX9Hook.Reset>(typeof(ImguiHookDx9), nameof(ResetImplStatic), resetPtr).Activate();
        }

        ~ImguiHookDx9()
        {
            ReleaseUnmanagedResources();
        }

        public bool IsApiSupported() => GetModuleHandle("d3d9.dll") != IntPtr.Zero;

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void Shutdown()
        {
            Debug.WriteLine($"[DX9 Shutdown] Shutdown");
            ImGui.ImGuiImplDX9Shutdown();
            _windowHandle = IntPtr.Zero;
            _device = IntPtr.Zero;
            _initialized = false;
            ImguiHook.Shutdown();
        }

        private void ReleaseUnmanagedResources()
        {
            if (_initialized)
                Shutdown();
        }

        private int ReleaseImpl(IntPtr device)
        {
            if (_releaseRecursionLock)
            {
                Debug.WriteLine($"[DX9 Release] Discarding via Recursion Lock");
                return _releaseHook.OriginalFunction.Value.Invoke(device);
            }

            _releaseRecursionLock = true;
            try
            {
                var count = _releaseHook.OriginalFunction.Value.Invoke(device);
                if (count == 0 && _device == device)
                {
                    Debug.WriteLine($"[DX9 Release] Shutting Down {(long)device:X}");
                    Shutdown();
                }

                return count;
            }
            finally
            {
                _releaseRecursionLock = false;
            }
        }

        private unsafe IntPtr EndSceneImpl(IntPtr device)
        {
            // With multi-viewports, ImGui might call EndScene again; so we need to prevent stack overflow here.
            if (_endSceneRecursionLock)
            {
                Debug.WriteLine($"[DX9 EndScene] Discarding via Recursion Lock");
                return _endSceneHook.OriginalFunction.Value.Invoke(device);
            }

            _endSceneRecursionLock = true;
            try
            {
                var dev = new Device(device);
                using var swapChain = dev.GetSwapChain(0);
                var windowHandle = dev.CreationParameters.HFocusWindow;
                var swapChainHandle = swapChain.PresentParameters.DeviceWindowHandle;
                windowHandle = windowHandle == IntPtr.Zero ? swapChainHandle : windowHandle;

                // Ignore windows which don't belong to us.
                if (!ImguiHook.CheckWindowHandle(windowHandle))
                {
                    Debug.WriteLine($"[DX9 EndScene] Discarding Window Handle {(long)windowHandle:X}");
                    return _endSceneHook.OriginalFunction.Value.Invoke(device);
                }

                if (!_initialized)
                {
                    _device = device;
                    _windowHandle = windowHandle;
                    if (_windowHandle == IntPtr.Zero) 
                        return _endSceneHook.OriginalFunction.Value.Invoke(device);

                    Debug.WriteLine($"[DX9 EndScene] Init, Window Handle {(long)windowHandle:X}");
                    ImguiHook.InitializeWithHandle(windowHandle);
                    ImGui.ImGuiImplDX9Init((void*)device);
                    _initialized = true;
                }

                ImGui.ImGuiImplDX9NewFrame();
                ImguiHook.NewFrame();
                using var drawData = ImGui.GetDrawData();
                ImGui.ImGuiImplDX9RenderDrawData(drawData);
                return _endSceneHook.OriginalFunction.Value.Invoke(device);
            }
            finally
            {
                _endSceneRecursionLock = false;
            }
        }

        private IntPtr ResetImpl(IntPtr device, PresentParameters* presentParameters)
        {
            // With multi-viewports, ImGui might call EndScene again; so we need to prevent stack overflow here.
            if (_resetRecursionLock)
            {
                Debug.WriteLine($"[DX9 Reset] Discarding via Recursion Lock");
                return _endSceneHook.OriginalFunction.Value.Invoke(device);
            }

            _resetRecursionLock = true;
            try
            {
                // Ignore windows which don't belong to us.
                if (!ImguiHook.CheckWindowHandle(presentParameters->DeviceWindowHandle))
                {
                    Debug.WriteLine($"[DX9 Reset] Discarding Window Handle {(long)presentParameters->DeviceWindowHandle:X}");
                    return _resetHook.OriginalFunction.Value.Invoke(device, presentParameters);
                }

                Debug.WriteLine($"[DX9 Reset] Reset with Handle {(long)presentParameters->DeviceWindowHandle:X}");
                ImGui.ImGuiImplDX9InvalidateDeviceObjects();
                var result = _resetHook.OriginalFunction.Value.Invoke(device, presentParameters);
                ImGui.ImGuiImplDX9CreateDeviceObjects();
                return result;
            }
            finally
            {
                _resetRecursionLock = false;
            }
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
        private static IntPtr EndSceneImplStatic(IntPtr device) => Instance.EndSceneImpl(device);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int ReleaseStatic(IntPtr device) => Instance.ReleaseImpl(device);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr ResetImplStatic(IntPtr device, PresentParameters* presentParameters) => Instance.ResetImpl(device, presentParameters);
        #endregion
    }
}
