using System;
using ImGuiNET;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.DirectX.Hooks;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

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
            ImGui.ImGui_ImplDX11_Shutdown();
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
            ImGui.ImGui_ImplDX11_InvalidateDeviceObjects();
        }

        private unsafe IntPtr PresentHook(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {
            var swapChain = new SwapChain(swapChainPtr);
            var device    = swapChain.GetDevice<Device>();
            
            if (!_initialized)
            {
                _windowHandle = swapChain.Description.OutputHandle;
                ImGui.ImGui_ImplDX11_Init((void*) device.NativePointer, (void*) device.ImmediateContext.NativePointer);

                var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
                _renderTargetView = new RenderTargetView(device, backBuffer);
                _initialized = true;
                backBuffer.Dispose();
            }

            
            ImGui.ImGui_ImplDX11_NewFrame();
            ImguiHook.NewFrame();
            device.ImmediateContext.OutputMerger.SetRenderTargets(_renderTargetView);
            ImGui.ImGui_ImplDX11_RenderDrawData(ImGui.GetDrawData());

            swapChain.Dispose();
            device.Dispose();
            return _presentHook.OriginalFunction(swapChainPtr, syncInterval, flags);
        }

        private void PostResizeBuffers(IntPtr swapChainPtr)
        {
            ImGui.ImGui_ImplDX11_CreateDeviceObjects();

            _renderTargetView?.Dispose();
            var swapChain = new SwapChain(swapChainPtr);
            var device = swapChain.GetDevice<Device>();
            var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
            _renderTargetView = new RenderTargetView(device, backBuffer);

            swapChain.Dispose();
            device.Dispose();
            backBuffer.Dispose();
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
