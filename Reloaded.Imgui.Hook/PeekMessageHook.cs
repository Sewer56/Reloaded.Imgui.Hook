using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Imgui.Hook.Misc;
using static Reloaded.Imgui.Hook.Misc.WindowMessage;
using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;

namespace Reloaded.Imgui.Hook
{
    /// <summary>
    /// Utility class that can be used to block inputs.
    /// </summary>
    public unsafe class PeekMessageHook
    {
        /// <summary>
        /// Set this to true to block keyboard input.
        /// </summary>
        public bool BlockKeyboardInput { get; set; }

        /// <summary>
        /// Set this to true to block mouse input.
        /// </summary>
        public bool BlockMouseInput { get; set; }

        private IHook<PeekMessage> _peekMessageAHook;
        private IHook<PeekMessage> _peekMessageWHook;
        private bool _isInitialized;

        /// <summary>
        /// Initializes the component.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            var hooks = SDK.Hooks;
            var user32 = Native.GetModuleHandle("user32.dll");

            var peekMessageA = Native.GetProcAddress(user32, "PeekMessageA");
            var peekMessageW = Native.GetProcAddress(user32, "PeekMessageW");
            _peekMessageAHook = hooks.CreateHook<PeekMessage>(PeekMessageAImpl, (long)peekMessageA).Activate();
            _peekMessageWHook = hooks.CreateHook<PeekMessage>(PeekMessageWImpl, (long)peekMessageW).Activate();
            _isInitialized = true;
        }

        private unsafe bool PeekMessageAImpl(Native.NativeMessage* msg, IntPtr hwnd, uint wmsgfiltermin, uint wmsgfiltermax, uint wRemoveMsg)
        {
            if (HandlePeekMessage(ref msg))
                return true;
            
            return _peekMessageAHook.OriginalFunction(msg, hwnd, wmsgfiltermin, wmsgfiltermax, wRemoveMsg);
        }

        private unsafe bool PeekMessageWImpl(Native.NativeMessage* msg, IntPtr hwnd, uint wmsgfiltermin, uint wmsgfiltermax, uint wRemoveMsg)
        {
            if (HandlePeekMessage(ref msg))
                return true;

            return _peekMessageWHook.OriginalFunction(msg, hwnd, wmsgfiltermin, wmsgfiltermax, wRemoveMsg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private unsafe bool HandlePeekMessage(ref Native.NativeMessage* msg)
        {
            // 0x0001 == PM_REMOVE
            bool shouldBlock = ShouldBlockEvent(msg);
            if (shouldBlock)
            {
                // We still some messages like 'WM_CHAR'.
                Native.TranslateMessage(msg);

                // Change message so ignored by parent window.
                msg->message = WM_NULL;
            }

            return shouldBlock;
        }

        private bool ShouldBlockEvent(Native.NativeMessage* nativeMessage)
        {
            bool isKeyboardMessage = nativeMessage->message >= WM_KEYFIRST && nativeMessage->message <= WM_KEYLAST;
            bool isMouseMessage    = nativeMessage->message >= WM_MOUSEFIRST && nativeMessage->message <= WM_MOUSELAST;

            if (nativeMessage->message == WM_INPUT)
                return true;

            return (isKeyboardMessage && BlockKeyboardInput) || (isMouseMessage && BlockMouseInput);
        }

        [Function(Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
        public delegate bool PeekMessage(Native.NativeMessage* msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    }
}
