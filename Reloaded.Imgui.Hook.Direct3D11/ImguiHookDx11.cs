using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook.Misc;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using static Reloaded.Imgui.Hook.Misc.Native;
using Device = SharpDX.Direct3D11.Device;

namespace Reloaded.Imgui.Hook.Direct3D11
{
    public unsafe class ImguiHookDx11 : IImguiHook
    {
        public static ImguiHookDx11 Instance { get; private set; }

        private IHook<DX11Hook.Present> _presentHook;
        private IHook<DX11Hook.ResizeBuffers> _resizeBuffersHook;
        private bool _initialized = false;
        private RenderTargetView _renderTargetView;

        private static readonly string[] _supportedDlls = new string[]
        {
            "d3d11.dll",
            "d3d11_1.dll",
            "d3d11_2.dll",
            "d3d11_3.dll",
            "d3d11_4.dll"
        };

        /*
         * In some cases (E.g. under DX9 + Viewports enabled), Dear ImGui might call
         * DirectX functions from within its internal logic.
         *
         * We put a lock on the current thread in order to prevent stack overflow.
         */
        private bool _presentRecursionLock = false;
        private bool _resizeRecursionLock = false;

        public ImguiHookDx11() { }

        public bool IsApiSupported()
        {
            foreach (var dll in _supportedDlls)
            {
                if (GetModuleHandle(dll) != IntPtr.Zero)
                    return true;
            }

            return false;
        }

        public void Initialize()
        {
            var presentPtr = (long)DX11Hook.DXGIVTable[(int)IDXGISwapChain.Present].FunctionPointer;
            var resizeBuffersPtr = (long)DX11Hook.DXGIVTable[(int)IDXGISwapChain.ResizeBuffers].FunctionPointer;

            _presentHook = SDK.Hooks.CreateHook<DX11Hook.Present>(typeof(ImguiHookDx11), nameof(PresentImplStatic), presentPtr).Activate();
            _resizeBuffersHook = SDK.Hooks.CreateHook<DX11Hook.ResizeBuffers>(typeof(ImguiHookDx11), nameof(ResizeBuffersImplStatic), resizeBuffersPtr).Activate();
            Instance = this;
        }
        ~ImguiHookDx11()
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
            {
                Debug.WriteLine($"[DX11 Dispose] Shutdown");
                ImGui.ImGuiImplDX11Shutdown();
            }
        }

        private IntPtr ResizeBuffersImpl(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags)
        {
            if (_resizeRecursionLock)
            {
                Debug.WriteLine($"[DX11 ResizeBuffers] Discarding via Recursion Lock");
                return _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
            }

            _resizeRecursionLock = true;
            try
            {
                var swapChain = new SwapChain(swapchainPtr);
                var windowHandle = swapChain.Description.OutputHandle;
                Debug.DebugWriteLine($"[DX11 ResizeBuffers] Window Handle {windowHandle}");

                // Ignore windows which don't belong to us.
                if (!ImguiHook.CheckWindowHandle(windowHandle))
                {
                    Debug.WriteLine($"[DX11 ResizeBuffers] Discarding Window Handle {windowHandle} due to Mismatch");
                    return _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
                }

                PreResizeBuffers();
                var result = _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
                PostResizeBuffers(swapchainPtr);

                return result;
            }
            finally
            {
                _resizeRecursionLock = false;
            }
        }

        private void PreResizeBuffers()
        {
            _renderTargetView?.Dispose();
            _renderTargetView = null;
            ImGui.ImGuiImplDX11InvalidateDeviceObjects();
        }

        private void PostResizeBuffers(IntPtr swapChainPtr)
        {
            ImGui.ImGuiImplDX11CreateDeviceObjects();

            _renderTargetView?.Dispose();
            var swapChain = new SwapChain(swapChainPtr);
            using var device = swapChain.GetDevice<Device>();
            using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
            _renderTargetView = new RenderTargetView(device, backBuffer);
        }

        private unsafe IntPtr PresentImpl(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {
            if (_presentRecursionLock)
            {
                Debug.WriteLine($"[DX11 Present] Discarding via Recursion Lock");
                return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
            }

            _presentRecursionLock = true;
            try
            {
                var swapChain = new SwapChain(swapChainPtr);
                var windowHandle = swapChain.Description.OutputHandle;

                // Ignore windows which don't belong to us.
                if (!ImguiHook.CheckWindowHandle(windowHandle))
                {
                    Debug.WriteLine($"[DX11 Present] Discarding Window Handle {windowHandle} due to Mismatch");
                    return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
                }

                // Initialise 
                using var device = swapChain.GetDevice<Device>();
                if (!_initialized)
                {
                    Debug.WriteLine($"[DX11 Present] Init DX11, Window Handle: {windowHandle:X}");
                    ImguiHook.InitializeWithHandle(windowHandle);
                    ImGui.ImGuiImplDX11Init((void*)device.NativePointer, (void*)device.ImmediateContext.NativePointer);

                    using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
                    _renderTargetView = new RenderTargetView(device, backBuffer);
                    _initialized = true;
                }

                ImGui.ImGuiImplDX11NewFrame();
                ImguiHook.NewFrame();
                device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);
                using var drawData = ImGui.GetDrawData();
                ImGui.ImGuiImplDX11RenderDrawData(drawData);

                return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
            }
            finally
            {
                _presentRecursionLock = false;
            }
        }
        
        public void Disable()
        {
            _presentHook?.Disable();
            _resizeBuffersHook?.Disable();
        }

        public void Enable()
        {
            _presentHook?.Enable();
            _resizeBuffersHook?.Enable();
        }

        #region Hook Functions
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr ResizeBuffersImplStatic(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags) => Instance.ResizeBuffersImpl(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr PresentImplStatic(IntPtr swapChainPtr, int syncInterval, PresentFlags flags) => Instance.PresentImpl(swapChainPtr, syncInterval, flags);
        #endregion
    }
}
