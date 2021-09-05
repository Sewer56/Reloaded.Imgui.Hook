using System;
using System.Windows.Forms;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Structs;
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
    public static class DX11Hook
    {
        /// <summary>
        /// Contains the DX11 Device VTable.
        /// </summary>
        public static IVirtualFunctionTable VTable { get; private set; }

        /// <summary>
        /// Contains the DX11 DXGI Swapchain VTable.
        /// </summary>
        public static IVirtualFunctionTable DXGIVTable { get; private set; }

        static DX11Hook()
        {
            // Define
            Device dx11Device;
            SwapChain dxgiSwapChain;
            var renderForm = new Form();

            // Get Table
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, GetSwapChainDescription(renderForm.Handle), out dx11Device, out dxgiSwapChain);
            VTable = SDK.Hooks.VirtualFunctionTableFromObject(dx11Device.NativePointer, Enum.GetNames(typeof(ID3D11Device)).Length);
            DXGIVTable = SDK.Hooks.VirtualFunctionTableFromObject(dxgiSwapChain.NativePointer, Enum.GetNames(typeof(IDXGISwapChain)).Length);

            // Cleanup
            dxgiSwapChain.Dispose();
            dx11Device.Dispose();
            renderForm.Dispose();
        }

        private static SwapChainDescription GetSwapChainDescription(IntPtr formHandle)
        {
            return new SwapChainDescription()
            {
                BufferCount = 1,
                IsWindowed = true,
                ModeDescription = new ModeDescription(640, 480, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                OutputHandle = formHandle,
                SampleDescription = new SampleDescription(1, 0),
            };
        }
        
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct Present { public FuncPtr<IntPtr, int, PresentFlags, IntPtr> Value; }
        
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct ResizeBuffers { public FuncPtr<IntPtr, uint, uint, uint, Format, uint, IntPtr> Value; }
    }
}
