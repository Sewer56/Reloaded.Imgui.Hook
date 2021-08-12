using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
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
        private IntPtr _windowHandle;
        private RenderTargetView _renderTargetView;
        
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
            PreResizeBuffers();
            var result = _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
            PostResizeBuffers(swapchainPtr);
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
            var swapChain = new SwapChain(swapChainPtr);
            using var device = swapChain.GetDevice<Device>();
            
            if (!_initialized)
            {
                _windowHandle = swapChain.Description.OutputHandle;
                ImGui.ImGuiImplDX11Init((void*) device.NativePointer, (void*) device.ImmediateContext.NativePointer);

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

        public IntPtr GetWindowHandle() => _windowHandle;

        #region Hook Functions
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr ResizeBuffersImplStatic(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags) => Instance.ResizeBuffersImpl(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
        
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static IntPtr PresentImplStatic(IntPtr swapChainPtr, int syncInterval, PresentFlags flags) => Instance.PresentImpl(swapChainPtr, syncInterval, flags);
        #endregion
    }
}
