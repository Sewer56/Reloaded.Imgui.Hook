using System;
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
    public class ImguiHookDX11 : IImguiHook
    {
        public ImguiHook ImguiHook { get; private set; }
        public DX11Hook Hook { get; private set; }

        private IHook<DX11Hook.Present> _presentHook;
        private IHook<DX11Hook.ResizeBuffers> _resizeBuffersHook;
        private bool _initialized = false;
        private IntPtr _windowHandle;
        private RenderTargetView _renderTargetView;

        public ImguiHookDX11(ImguiHook hook)
        {
            ImguiHook = hook;
            Hook = new DX11Hook(SDK.Hooks);
            _presentHook = Hook.DXGIVTable.CreateFunctionHook<DX11Hook.Present>((int)IDXGISwapChain.Present, PresentHook).Activate();
            _resizeBuffersHook = Hook.DXGIVTable.CreateFunctionHook<DX11Hook.ResizeBuffers>((int)IDXGISwapChain.ResizeBuffers, ResizeBuffers).Activate();
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
            ImGui.ImGuiImplDX11Shutdown();
        }

        private IntPtr ResizeBuffers(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags)
        {
            PreResizeBuffers();
            var result = _resizeBuffersHook.OriginalFunction(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
            PostResizeBuffers(swapchainPtr);
            return result;
        }

        private void PreResizeBuffers()
        {
            _renderTargetView?.Dispose();
            _renderTargetView = null;
            ImGui.ImGuiImplDX11InvalidateDeviceObjects();
        }

        private unsafe IntPtr PresentHook(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {
            using var swapChain = new SwapChain(swapChainPtr);
            using var device    = swapChain.GetDevice<Device>();
            
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
            
            return _presentHook.OriginalFunction(swapChainPtr, syncInterval, flags);
        }

        private void PostResizeBuffers(IntPtr swapChainPtr)
        {
            ImGui.ImGuiImplDX11CreateDeviceObjects();

            _renderTargetView?.Dispose();
            using var swapChain = new SwapChain(swapChainPtr);
            using var device = swapChain.GetDevice<Device>();
            using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
            _renderTargetView = new RenderTargetView(device, backBuffer);
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
    }
}
