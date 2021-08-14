using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DearImguiSharp;
using Reloaded.Imgui.Hook.DirectX;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook.Misc;
using Debug = Reloaded.Imgui.Hook.Misc.Debug;

namespace Reloaded.Imgui.Hook
{
    public static class ImguiHook
    {
        /// <summary>
        /// User supplied function to render the imgui UI.
        /// </summary>
        public static Action Render { get; private set; }

        /// <summary>
        /// Current hook for the render window's WndProc.
        /// </summary>
        public static WndProcHook WndProcHook { get; private set; }

        /// <summary>
        /// Abstracts the current dear imgui implementations (DX9, DX11)
        /// </summary>
        public static List<IImguiHook> Implementations { get; private set; }
        
        /// <summary>
        /// The current ImGui context.
        /// </summary>
        public static ImGuiContext Context { get; private set; }

        /// <summary>
        /// Handle of the window being rendered.
        /// </summary>
        public static IntPtr WindowHandle { get; private set; }

        /// <summary>
        /// True if the hook has been initialized, else false.
        /// </summary>
        public static bool Initialized { get; private set; }

        /// <summary>
        /// Allows access to ImGui IO Settings.
        /// </summary>
        public static ImGuiIO IO { get; private set; }

        /// <summary>
        /// The options with which this hook has been created with.
        /// </summary>
        public static ImguiHookOptions Options { get; private set; }

        private static bool _created = false;

        /// <summary>
        /// Creates a new hook given the Reloaded.Hooks library.
        /// The library will hook to the main window.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        /// <param name="options">The options with which to initialise the hook.</param>
        public static async Task Create(Action render, ImguiHookOptions options = null)
        {
            if (_created)
                return;

            var dxVersion = await Utility.GetDXVersion().ConfigureAwait(false);
            Create(render, IntPtr.Zero, dxVersion, options);
        }

        /// <summary>
        /// Creates a new hook given the Reloaded.Hooks library.
        /// The library will hook to the main window.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        /// <param name="windowHandle">Handle of the window to draw on.</param>
        /// <param name="options">The options with which to initialise the hook.</param>
        public static async Task Create(Action render, IntPtr windowHandle, ImguiHookOptions options = null)
        {
            if (_created)
                return;

            var dxVersion = await Utility.GetDXVersion().ConfigureAwait(false);
            Create(render, windowHandle, dxVersion, options);
        }

        /// <summary>
        /// Creates a new ImGui hook.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        /// <param name="windowHandle">Handle to the window to render on. Pass IntPtr.Zero to select main window.</param>
        /// <param name="version">DirectX version to handle.</param>
        /// <param name="options">The options with which to initialise the hook.</param>
        public static void Create(Action render, IntPtr windowHandle, Direct3DVersion version, ImguiHookOptions options = null)
        {
            _created = true;
            Render = render;
            WindowHandle = windowHandle;
            Context = ImGui.CreateContext(null);
            IO = Context.IO;
            Options = options ?? new ImguiHookOptions();

            if (Options.EnableViewports)
                IO.ConfigFlags |= (int)ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable;

            ImGui.StyleColorsDark(null);
            Implementations = new List<IImguiHook>();
            if (Utility.IsD3D11(version))
                Implementations.Add(ImguiHookDX11.Instance);
            if (Utility.IsD3D9(version))
                Implementations.Add(ImguiHookDX9.Instance);

            if (Implementations.Count <= 0)
            {
                Disable();
                throw new Exception("Unsupported or not found any compatible DirectX Implementation(s).");
            }
        }

        /// <summary>
        /// Destroys the current instance of <see cref="ImguiHook"/>.
        /// Use if you don't plan on using the hook again, such as when unloading a mod.
        /// </summary>
        public static void Destroy()
        {
            Disable();

            if (Initialized)
            {
                Debug.WriteLine($"[ImguiHook Destroy] Win32 Shutdown");
                ImGui.ImGuiImplWin32Shutdown();
            }

            if (Implementations != null)
            {
                foreach (var implementation in Implementations)
                {
                    implementation?.Dispose();
                }
            }

            Debug.WriteLine($"[ImguiHook Destroy] Destroy Context");

            ImGui.DestroyContext(Context);
            Context?.Dispose();

            Render = null;
            Implementations = null;
            Context = null;
            WndProcHook = null;
            WindowHandle = IntPtr.Zero;
            Initialized = false;

            _created = false;
        }

        /// <summary>
        /// Enables the <see cref="ImguiHook"/> after it has been temporarily disabled.
        /// </summary>
        public static void Enable()
        {
            WndProcHook?.Enable(); 
            if (Implementations == null)
                return;

            foreach (var implementation in Implementations)
                implementation?.Enable();
        }

        /// <summary>
        /// Disables the <see cref="ImguiHook"/> temporarily.
        /// </summary>
        public static void Disable()
        {
            WndProcHook?.Disable();
            if (Implementations == null) 
                return;

            foreach (var implementation in Implementations)
                implementation?.Disable();
        }

        /// <summary>
        /// Hooks WndProc to allow for input for ImGui
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new []{ typeof(CallConvStdcall) })]
        private static unsafe IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            ImGui.ImplWin32_WndProcHandler((void*) hWnd, msg, wParam, lParam);

            if (Options.IgnoreWindowUnactivate)
            {
                var message = (WindowMessage) msg;
                switch (message)
                {
                    case WindowMessage.WM_KILLFOCUS:
                        return IntPtr.Zero;

                    case WindowMessage.WM_ACTIVATE:
                    case WindowMessage.WM_ACTIVATEAPP:
                        if (wParam == IntPtr.Zero)
                            return IntPtr.Zero;

                        break;
                }
            }

            return WndProcHook.Hook.OriginalFunction.Value.Invoke(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Checks if the provided window handle matches the window handle associated with this context.
        /// If not initialised, accepts only IntPtr.Zero
        /// </summary>
        /// <param name="windowHandle">The window handle.</param>
        internal static bool CheckWindowHandle(IntPtr windowHandle)
        {
            // Check for exact handle.
            if (windowHandle != IntPtr.Zero)
                return windowHandle == WindowHandle || !Initialized;

            return false;
        }


        /// <summary>
        /// Called from renderer implementation, renders a new frame.
        /// </summary>
        internal static unsafe void InitializeWithHandle(IntPtr windowHandle)
        {
            if (!Initialized)
            {
                WindowHandle = windowHandle;
                if (WindowHandle == IntPtr.Zero)
                    return;

                Debug.WriteLine($"[ImguiHook] Init with Window Handle {(long)WindowHandle:X}");
                ImGui.ImGuiImplWin32Init(WindowHandle);
                var wndProcHandlerPtr = (IntPtr)SDK.Hooks.Utilities.GetFunctionPointer(typeof(ImguiHook), nameof(WndProcHandler));
                WndProcHook = WndProcHook.Create(WindowHandle, Unsafe.As<IntPtr, WndProcHook.WndProc>(ref wndProcHandlerPtr));
                Initialized = true;
            }
        }

        /// <summary>
        /// Called from renderer implementation, renders a new frame.
        /// </summary>
        internal static unsafe void NewFrame()
        {
            ImGui.ImGuiImplWin32NewFrame();
            ImGui.NewFrame();
            Render();
            ImGui.EndFrame();
            ImGui.Render();

            if ((IO.ConfigFlags & (int)ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable) > 0)
            {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault(IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
