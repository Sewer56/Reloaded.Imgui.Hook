﻿using System;
using System.Windows.Forms;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Structs;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Imgui.Hook.Direct3D9.Definitions;
using Reloaded.Imgui.Hook.Misc;
using SharpDX.Direct3D9;
using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;

namespace Reloaded.Imgui.Hook.Direct3D9
{
    /// <summary>
    /// Provides access to DirectX 9 functions.
    /// </summary>
    public static class DX9Hook
    {
        /// <summary>
        /// Contains the DX9 device VTable.
        /// </summary>
        public static IVirtualFunctionTable DeviceVTable { get; private set; }

        /// <summary>
        /// Contains the DX9 VTable.
        /// </summary>
        public static IVirtualFunctionTable Direct3D9VTable { get; private set; }

        static DX9Hook()
        {
            // Obtain the pointer to the IDirect3DDevice9 instance by creating our own blank windows form and creating a  
            // IDirect3DDevice9 targeting that form. The returned device should be the same one as used by the program.
            using var direct3D = new Direct3D();
            using var renderForm = new Form();
            using var device = new Device(direct3D, 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, GetParameters(direct3D, renderForm.Handle));
            Direct3D9VTable = SDK.Hooks.VirtualFunctionTableFromObject(direct3D.NativePointer, Enum.GetNames(typeof(IDirect3D9)).Length);
            DeviceVTable = SDK.Hooks.VirtualFunctionTableFromObject(device.NativePointer, Enum.GetNames(typeof(IDirect3DDevice9)).Length);
        }

        private static PresentParameters GetParameters(Direct3D d3d, IntPtr windowHandle)
        {
            var mode = d3d.GetAdapterDisplayMode(0);
            return new PresentParameters()
            {
                BackBufferWidth = mode.Width,
                BackBufferHeight = mode.Height,
                BackBufferFormat = mode.Format,
                DeviceWindowHandle = windowHandle,
                SwapEffect = SwapEffect.Discard,
                Windowed = true,
                PresentationInterval = PresentInterval.Immediate
            };
        }

        /// <summary>
        /// Defines the IDirect3DDevice9.EndScene function, allowing us to render ontop of the DirectX instance.
        /// </summary>
        /// <param name="device">Pointer to the individual Direct3D9 device.</param>
        [FunctionHookOptions(PreferRelativeJump = true)]
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct EndScene { public FuncPtr<IntPtr, IntPtr> Value; }

        /// <summary>
        /// Defines the IDirect3DDevice9.EndScene function, allowing us to render ontop of the DirectX instance.
        /// </summary>
        /// <param name="device">Pointer to the individual Direct3D9 device.</param>
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct Release { public FuncPtr<IntPtr, int> Value; }

        /// <summary>
        /// Defines the IDirect3DDevice9.Reset function, called when the resolution or Windowed/Fullscreen state changes.
        /// changes.
        /// </summary>
        /// <param name="device">Pointer to the individual Direct3D9 device.</param>
        /// <param name="presentParameters">Pointer to a D3DPRESENT_PARAMETERS structure, describing the new presentation parameters.</param>
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct Reset { public FuncPtr<IntPtr, BlittablePtr<PresentParameters>, IntPtr> Value; }

        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct CreateDevice { public FuncPtr<IntPtr, uint, DeviceType, IntPtr, CreateFlags, BlittablePtr<PresentParameters>, BlittablePtrPtr<int>, IntPtr> Value; }
    }
}
