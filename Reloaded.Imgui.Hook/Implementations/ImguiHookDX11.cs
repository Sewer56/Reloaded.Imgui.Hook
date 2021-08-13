using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
using Reloaded.Imgui.Hook.Misc;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;
using ID3D11Device = DearImguiSharp.ID3D11Device;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookDX11 : IImguiHook
    {
        public static ImguiHookDX11 Instance { get; private set; } = new ImguiHookDX11();
        
        private IHook<DX11Hook.Present> _presentHook;
        private IHook<DX11Hook.ResizeBuffers> _resizeBuffersHook;
        private bool _initialized = false;
        private RenderTargetView _renderTargetView;
        private bool _presentRecursionLock = false;
        private bool _resizeRecursionLock = false;

        public ImguiHookDX11()
        {
            var presentPtr = (long) DX11Hook.DXGIVTable[(int) IDXGISwapChain.Present].FunctionPointer;
            var resizeBuffersPtr = (long) DX11Hook.DXGIVTable[(int) IDXGISwapChain.ResizeBuffers].FunctionPointer;

            _presentHook = SDK.Hooks.CreateHook<DX11Hook.Present>(typeof(ImguiHookDX11), nameof(PresentImplStatic), presentPtr).Activate();
            _resizeBuffersHook = SDK.Hooks.CreateHook<DX11Hook.ResizeBuffers>(typeof(ImguiHookDX11), nameof(ResizeBuffersImplStatic), resizeBuffersPtr).Activate();
        }

        ~ImguiHookDX11()
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
            _renderTargetView?.Dispose();
            if (_initialized)
                ImGui.ImGuiImplDX11Shutdown();
        }

        private IntPtr ResizeBuffersImpl(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags)
        {
            // Just in case Dear ImGui tries calling this again, like with DX9.
            if (_resizeRecursionLock)
                return _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);

            _resizeRecursionLock = true;

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

            _resizeRecursionLock = false;
            return result;
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
            // Just in case Dear ImGui tries calling this again, like with DX9.
            if (_presentRecursionLock)
                return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);

            _presentRecursionLock = true;
            var swapChain = new SwapChain(swapChainPtr);
            var windowHandle = swapChain.Description.OutputHandle;
            Debug.DebugWriteLine($"[DX11 Present] Window Handle {windowHandle}");

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
                ImGui.ImGuiImplDX11Init((void*) device.NativePointer, (void*) device.ImmediateContext.NativePointer);

                using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
                _renderTargetView = new RenderTargetView(device, backBuffer);
                _initialized = true;
            }

            ImGui.ImGuiImplDX11NewFrame();
            ImguiHook.NewFrame(windowHandle);
            device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);
            using var drawData = ImGui.GetDrawData();
            ImGui.ImGuiImplDX11RenderDrawData(drawData);

            _presentRecursionLock = false;
            return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
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
