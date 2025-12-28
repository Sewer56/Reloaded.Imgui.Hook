using System;
using System.Collections.Generic;

namespace Reloaded.Imgui.Hook
{
    public class ImguiHookOptions
    {
        /// <summary>
        /// [Experimental! + Initialisation Only!]
        /// Enables the viewports feature of Dear ImGui; which puts individual ImGui windows onto invisible windows, allowing
        /// them to escape the program region/area.
        /// </summary>
        public bool EnableViewports = false;

        /// <summary>
        /// [Real Time]
        /// Tries to suppress window deactivation message to the application/game.
        /// Sometimes necessary to stop the application from pausing when <see cref="EnableViewports"/> is turned on.
        /// </summary>
        public bool IgnoreWindowUnactivate = false;
        
        /// <summary>
        /// [Initialization Only!]
        /// Specifies a custom WndProc handler for specialized message processing.
        /// <br/>
        /// <b>Requirements:</b>
        /// <list type="bullet">
        /// <item>Calling Convention: <c>stdcall</c> (C#: <c>[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]</c>)</item>
        /// <item>Signature: <c>IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)</c></item>
        /// </list>
        /// <b>Note:</b> The implementation is responsible for invoking <see cref="DearImguiSharp.ImGui.ImplWin32_WndProcHandler"/>, 
        /// the original WndProc (via <c>WndProcHook.Instance.Hook.OriginalFunction.Value.Invoke</c>), and returning the appropriate result.
        /// </summary>
        public IntPtr? CustomWndProcHandlerPointer = null;

        /// <summary>
        /// The individual list of implementations.
        /// </summary>
        public List<Implementations.IImguiHook> Implementations { get; set; }
    }
}
