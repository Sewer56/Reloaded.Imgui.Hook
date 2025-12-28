using System;
using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Structs;
using Reloaded.Hooks.Definitions.X64;
using static Reloaded.Imgui.Hook.Misc.Native;
using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;

namespace Reloaded.Imgui.Hook.OpenGL3
{
    /// <summary>
    /// Provides access to OpenGL functions for hooking.
    /// </summary>
    public static class OpenGL3Hook
    {
        /// <summary>
        /// Function pointer to wglSwapBuffers from opengl32.dll
        /// </summary>
        public static IntPtr WglSwapBuffersPtr { get; private set; }

        /// <summary>
        /// Function pointer to SwapBuffers from gdi32.dll
        /// </summary>
        public static IntPtr SwapBuffersPtr { get; private set; }

        static OpenGL3Hook()
        {
            // Get function pointers for both swap buffer functions
            var opengl32Handle = GetModuleHandle("opengl32.dll");
            var gdi32Handle = GetModuleHandle("gdi32.dll");

            if (opengl32Handle != IntPtr.Zero)
            {
                WglSwapBuffersPtr = GetProcAddress(opengl32Handle, "wglSwapBuffers");
            }

            if (gdi32Handle != IntPtr.Zero)
            {
                SwapBuffersPtr = GetProcAddress(gdi32Handle, "SwapBuffers");
            }
        }

        /// <summary>
        /// Defines the wglSwapBuffers function from opengl32.dll
        /// </summary>
        [FunctionHookOptions(PreferRelativeJump = true)]
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct WglSwapBuffers { public FuncPtr<IntPtr, int> Value; }

        /// <summary>
        /// Defines the SwapBuffers function from gdi32.dll
        /// </summary>
        [FunctionHookOptions(PreferRelativeJump = true)]
        [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public struct SwapBuffers { public FuncPtr<IntPtr, int> Value; }
    }
}

