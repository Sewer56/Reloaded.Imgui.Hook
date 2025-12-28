using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook.OpenGL;
using static Reloaded.Imgui.Hook.Misc.Native;
using Debug = Reloaded.Imgui.Hook.Misc.Debug;

namespace Reloaded.Imgui.Hook.Implementations
{
    public unsafe class ImguiHookOpenGL : IImguiHook
    {
        public static ImguiHookOpenGL Instance { get; private set; }

        private IHook<OpenGLHook.WglSwapBuffers> _wglSwapBuffersHook;
        private IHook<OpenGLHook.SwapBuffers> _swapBuffersHook;
        private bool _initialized = false;
        private bool _wglUsed = false;
        private bool _gdiUsed = false;
        private IntPtr _windowHandle;
        private IntPtr _deviceContext;

        /*
         * In some cases (E.g. with Viewports enabled), Dear ImGui might call
         * OpenGL functions from within its internal logic.
         *
         * We put a lock on the current thread in order to prevent stack overflow.
         */
        private bool _wglSwapBuffersRecursionLock = false;
        private bool _swapBuffersRecursionLock = false;
        private bool _isRendering = false;

        public ImguiHookOpenGL() { }

        public void Initialize()
        {
            Instance = this;

            ImGui.GetIO().SetPlatformImeDataFn = (viewport, data) =>
            {

            };

            // Hook wglSwapBuffers if available
            if (OpenGLHook.WglSwapBuffersPtr != IntPtr.Zero)
            {
                var wglSwapBuffersPtr = (long)OpenGLHook.WglSwapBuffersPtr;
                _wglSwapBuffersHook = SDK.Hooks.CreateHook<OpenGLHook.WglSwapBuffers>(typeof(ImguiHookOpenGL), nameof(WglSwapBuffersImplStatic), wglSwapBuffersPtr).Activate();
                Debug.WriteLine($"[OpenGL Initialize] Hooked wglSwapBuffers at 0x{wglSwapBuffersPtr:X}");
            }

            // Hook SwapBuffers if available
            if (OpenGLHook.SwapBuffersPtr != IntPtr.Zero)
            {
                var swapBuffersPtr = (long)OpenGLHook.SwapBuffersPtr;
                _swapBuffersHook = SDK.Hooks.CreateHook<OpenGLHook.SwapBuffers>(typeof(ImguiHookOpenGL), nameof(SwapBuffersImplStatic), swapBuffersPtr).Activate();
                Debug.WriteLine($"[OpenGL Initialize] Hooked SwapBuffers at 0x{swapBuffersPtr:X}");
            }
        }

        ~ImguiHookOpenGL()
        {
            ReleaseUnmanagedResources();
        }

        public bool IsApiSupported()
        {
            var opengl32 = GetModuleHandle("opengl32.dll");
            var gdi32 = GetModuleHandle("gdi32.dll");
            return opengl32 != IntPtr.Zero || gdi32 != IntPtr.Zero;
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
            _deviceContext = IntPtr.Zero;
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
                Debug.WriteLine($"[OpenGL wglSwapBuffers] Discarding via Recursion Lock");
                return _wglSwapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }

            _wglSwapBuffersRecursionLock = true;
            try
            {
                return SwapBuffersCommon(hdc, () => _wglSwapBuffersHook.OriginalFunction.Value.Invoke(hdc), "wglSwapBuffers");
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
                Debug.WriteLine($"[OpenGL SwapBuffers] Discarding via Recursion Lock");
                return _swapBuffersHook.OriginalFunction.Value.Invoke(hdc);
            }

            _swapBuffersRecursionLock = true;
            try
            {
                return SwapBuffersCommon(hdc, () => _swapBuffersHook.OriginalFunction.Value.Invoke(hdc), "SwapBuffers");
            }
            finally
            {
                _swapBuffersRecursionLock = false;
            }
        }

        private int SwapBuffersCommon(IntPtr hdc, Func<int> originalFunction, string hookName)
        {
            // Get window handle from device context
            var windowHandle = WindowFromDC(hdc);

            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(windowHandle))
            {
                Debug.WriteLine($"[OpenGL {hookName}] Discarding Window Handle {(long)windowHandle:X}");
                return originalFunction();
            }

            // Prevent double rendering if an app uses both methods or one calls the other.
            if (_isRendering)
                return originalFunction();

            _isRendering = true;
            try
            {
                if (!_initialized)
                {
                    _deviceContext = hdc;
                    _windowHandle = windowHandle;
                    if (_windowHandle == IntPtr.Zero)
                        return originalFunction();

                    Debug.WriteLine($"[OpenGL {hookName}] Init, Window Handle {(long)windowHandle:X}");
                    ImguiHook.InitializeWithHandle(windowHandle);

                    // Initialize OpenGL3 backend with GLSL version string
                    // Using "#version 130" for OpenGL 3.0+ compatibility
                    ImGui.ImGuiImplOpenGL3Init("#version 130");
                    _initialized = true;
                }

                // Log which method is being used (once per method)
                if (hookName == "wglSwapBuffers" && !_wglUsed)
                {
                    _wglUsed = true;
                    Debug.WriteLine($"[OpenGL] Application is using wglSwapBuffers");
                }
                else if (hookName == "SwapBuffers" && !_gdiUsed)
                {
                    _gdiUsed = true;
                    Debug.WriteLine($"[OpenGL] Application is using GDI SwapBuffers");
                }

                ImGui.ImGuiImplOpenGL3NewFrame();
                ImguiHook.NewFrame();
                using var drawData = ImGui.GetDrawData();
                ImGui.ImGuiImplOpenGL3RenderDrawData(drawData);

                return originalFunction();
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

