using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.OpenGL3;
using static Reloaded.Imgui.Hook.Misc.Native;
using Debug = Reloaded.Imgui.Hook.Misc.Debug;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookOpenGL3 : IImguiHook
    {
        public static ImguiHookOpenGL3 Instance { get; private set; }

        private IHook<OpenGL3Hook.WglSwapBuffers> _wglSwapBuffersHook;
        private IHook<OpenGL3Hook.SwapBuffers> _swapBuffersHook;
        private bool _initialized = false;
        private IntPtr _windowHandle;

        /*
         * In some cases (E.g. with Viewports enabled), Dear ImGui might call
         * OpenGL functions from within its internal logic.
         *
         * We put a lock on the current thread in order to prevent stack overflow.
         */
        private bool _wglSwapBuffersRecursionLock = false;
        private bool _swapBuffersRecursionLock = false;
        private bool _isRendering = false;

        public ImguiHookOpenGL3() { }

        public void Initialize()
        {
            Instance = this;

            // Avoid deadlocks by disabling IME functionality
            // see: https://github.com/ocornut/imgui/issues/5535
            ImGui.GetIO().SetPlatformImeDataFn = (viewport, data) =>
            {

            };

            // Hook wglSwapBuffers if available
            if (OpenGL3Hook.WglSwapBuffersPtr != IntPtr.Zero)
            {
                var wglSwapBuffersPtr = (long)OpenGL3Hook.WglSwapBuffersPtr;
                _wglSwapBuffersHook = SDK.Hooks.CreateHook<OpenGL3Hook.WglSwapBuffers>(typeof(ImguiHookOpenGL3), nameof(WglSwapBuffersImplStatic), wglSwapBuffersPtr).Activate();
                Debug.WriteLine($"[OpenGL Initialize] Hooked wglSwapBuffers at 0x{wglSwapBuffersPtr:X}");
            }

            // Hook SwapBuffers if available
            if (OpenGL3Hook.SwapBuffersPtr != IntPtr.Zero)
            {
                var swapBuffersPtr = (long)OpenGL3Hook.SwapBuffersPtr;
                _swapBuffersHook = SDK.Hooks.CreateHook<OpenGL3Hook.SwapBuffers>(typeof(ImguiHookOpenGL3), nameof(SwapBuffersImplStatic), swapBuffersPtr).Activate();
                Debug.WriteLine($"[OpenGL Initialize] Hooked SwapBuffers at 0x{swapBuffersPtr:X}");
            }
        }

        ~ImguiHookOpenGL3()
        {
            ReleaseUnmanagedResources();
        }

        public bool IsApiSupported()
        {
            return GetModuleHandle("opengl32.dll") != IntPtr.Zero;
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void Shutdown()
        {
            Debug.WriteLine($"[OpenGL Shutdown] Shutdown");
            if (_initialized)
            {
                ImGui.ImGuiImplOpenGL3Shutdown();
                _initialized = false;
            }
            _windowHandle = IntPtr.Zero;
            ImguiHook.Shutdown();
        }

        private void ReleaseUnmanagedResources()
        {
            if (_initialized)
                Shutdown();
        }

        private int WglSwapBuffersImpl(IntPtr hdc)
        {
            // With multi-viewports, ImGui might call SwapBuffers again; so we need to prevent stack overflow here.
            if (_wglSwapBuffersRecursionLock)
            {
                return _wglSwapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }

            _wglSwapBuffersRecursionLock = true;
            try
            {
                SwapBuffersCommon(hdc, "wglSwapBuffers");
                return _wglSwapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }
            finally
            {
                _wglSwapBuffersRecursionLock = false;
            }
        }

        private int SwapBuffersImpl(IntPtr hdc)
        {
            // With multi-viewports, ImGui might call SwapBuffers again; so we need to prevent stack overflow here.
            if (_swapBuffersRecursionLock)
            {
                return _swapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }

            _swapBuffersRecursionLock = true;
            try
            {
                SwapBuffersCommon(hdc, "SwapBuffers");
                return _swapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }
            finally
            {
                _swapBuffersRecursionLock = false;
            }
        }

        private void SwapBuffersCommon(IntPtr hdc, string hookName)
        {
            // Get window handle from device context
            var windowHandle = WindowFromDC(hdc);

            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(windowHandle))
            {
                Debug.WriteLine($"[OpenGL {hookName}] Discarding Window Handle {(long)windowHandle:X}");
                return;
            }

            // Prevent double rendering if an app uses both methods or one calls the other.
            if (_isRendering)
                return;

            _isRendering = true;
            try
            {
                if (!_initialized)
                {
                    _windowHandle = windowHandle;
                    if (_windowHandle == IntPtr.Zero)
                        return;

                    Debug.WriteLine($"[OpenGL {hookName}] Init, Window Handle {(long)windowHandle:X}");
                    ImguiHook.InitializeWithHandle(windowHandle);

                    // Initialize OpenGL3 backend with GLSL version string
                    // Using "#version 130" for OpenGL 3.0+ compatibility
                    ImGui.ImGuiImplOpenGL3Init("#version 130");
                    _initialized = true;
                }

                ImGui.ImGuiImplOpenGL3NewFrame();
                ImguiHook.NewFrame();
                using var drawData = ImGui.GetDrawData();
                ImGui.ImGuiImplOpenGL3RenderDrawData(drawData);
            }
            finally
            {
                _isRendering = false;
            }
        }

        public void Disable()
        {
            _wglSwapBuffersHook?.Disable();
            _swapBuffersHook?.Disable();
        }

        public void Enable()
        {
            _wglSwapBuffersHook?.Enable();
            _swapBuffersHook?.Enable();
        }

        #region Hook Functions
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int WglSwapBuffersImplStatic(IntPtr hdc) => Instance.WglSwapBuffersImpl(hdc);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int SwapBuffersImplStatic(IntPtr hdc) => Instance.SwapBuffersImpl(hdc);
        #endregion
    }
}

