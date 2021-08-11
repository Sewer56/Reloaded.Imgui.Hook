using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;
using Device = SharpDX.Direct3D11.Device;

namespace Reloaded.Imgui.Hook.DirectX.Hooks
{
    /// <summary>
    /// Provides access to DirectX 11 functions.
    /// </summary>
    public class DX11Hook
    {
        /// <summary>
        /// Contains the DX11 Device VTable.
        /// </summary>
        public IVirtualFunctionTable VTable { get; private set; }

        /// <summary>
        /// Contains the DX11 DXGI Swapchain VTable.
        /// </summary>
        public IVirtualFunctionTable DXGIVTable { get; private set; }

        public DX11Hook(IReloadedHooks _hooks)
        {
            // Define
            Device dx11Device;
            SwapChain dxgiSwapChain;
            var renderForm = new Form();

            // Get Table
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, GetSwapChainDescription(renderForm.Handle), out dx11Device, out dxgiSwapChain);
            VTable = _hooks.VirtualFunctionTableFromObject(dx11Device.NativePointer, Enum.GetNames(typeof(ID3D11Device)).Length);
            DXGIVTable = _hooks.VirtualFunctionTableFromObject(dxgiSwapChain.NativePointer, Enum.GetNames(typeof(IDXGISwapChain)).Length);

            // Cleanup
            dxgiSwapChain.Dispose();
            dx11Device.Dispose();
            renderForm.Dispose();
        }

        private SwapChainDescription GetSwapChainDescription(IntPtr formHandle)
        {
            return new SwapChainDescription()
            {
                BufferCount = 1,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                OutputHandle = formHandle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };
        }


        /// <summary>
        /// Defines the IDXGISwapChain.Present function, used to show the rendered image right to the user.
        /// </summary>
        /// <param name="swapChainPtr">The pointer to the actual swapchain, `this` object.</param>
        /// <param name="syncInterval">An integer that specifies how to synchronize presentation of a frame with the vertical blank.</param>
        /// <param name="flags">An integer value that contains swap-chain presentation options. These options are defined by the DXGI_PRESENT constants.</param>
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public delegate IntPtr Present(IntPtr swapChainPtr, int syncInterval, PresentFlags flags);
        
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public delegate IntPtr ResizeTarget(IntPtr swapChainPtr, ref ModeDescription newTargetParameters);
        
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public delegate IntPtr ResizeBuffers(IntPtr swapChainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapChainFlags);
    }
}
