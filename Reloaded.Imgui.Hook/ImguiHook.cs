using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DearImguiSharp;
using Reloaded.Imgui.Hook.DirectX;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook.Misc;

namespace Reloaded.Imgui.Hook
{
    public class ImguiHook : IDisposable
    {
        /// <summary>
        /// User supplied function to render the imgui UI.
        /// </summary>
        public Action Render { get; private set; }

        /// <summary>
        /// Current hook for the render window's WndProc.
        /// </summary>
        public WndProcHook WndProcHook { get; private set; }

        /// <summary>
        /// Abstracts the current dear imgui implementation (DX9, DX11)
        /// </summary>
        public IImguiHook Implementation { get; private set; }
        
        /// <summary>
        /// The current ImGui context.
        /// </summary>
        public ImGuiContext Context { get; private set; }

        /// <summary>
        /// Handle of the window being rendered.
        /// </summary>
        public IntPtr WindowHandle { get; private set; }

        /// <summary>
        /// True if the hook has been initialized, else false.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Message filter for the PeekMessage WinAPI function.
        /// Can be used to filter inputs away from target application.
        /// </summary>
        public PeekMessageHook InputBlocker { get; private set; } 

        // Construction/Destruction
        private ImguiHook(Action render, IntPtr windowHandle)
        {
            Render = render;
            WindowHandle = windowHandle;
            Context = ImGui.CreateContext(null);
            InputBlocker = new PeekMessageHook();
            ImGui.StyleColorsDark(null);
        }

        ~ImguiHook()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                Implementation?.Dispose();
                Context?.Dispose();
            }
        }

        private void ReleaseUnmanagedResources()
        {
            ImGui.ImGuiImplWin32Shutdown();
            ImGui.DestroyContext(Context);
        }

        /// <summary>
        /// Hooks WndProc to allow for input for ImGui
        /// </summary>
        private unsafe IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            ImGui.ImplWin32_WndProcHandler((void*) hWnd, msg, wParam, lParam);
            return WndProcHook.Hook.OriginalFunction(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Called from renderer implementation, renders a new frame.
        /// </summary>
        internal void NewFrame()
        {
            if (!Initialized)
            {
                if (WindowHandle == IntPtr.Zero)
                    WindowHandle = Implementation.GetWindowHandle();

                if (WindowHandle == IntPtr.Zero)
                    return;

                ImGui.ImGuiImplWin32Init(WindowHandle); 
                WndProcHook = new WndProcHook(WindowHandle, WndProcHandler);
                Initialized = true;
            }

            ImGui.ImGuiImplWin32NewFrame();
            ImGui.NewFrame();
            Render();
            ImGui.Render();
        }

        /// <summary>
        /// Creates a new hook given the Reloaded.Hooks library.
        /// The library will hook to the main window.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        public static async Task<ImguiHook> Create(Action render)
        {
            var dxVersion = await Utility.GetDXVersion().ConfigureAwait(false);
            return Create(render, IntPtr.Zero, dxVersion);
        }

        /// <summary>
        /// Creates a new hook given the Reloaded.Hooks library.
        /// The library will hook to the main window.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        /// <param name="windowHandle">Handle of the window to draw on.</param>
        public static async Task<ImguiHook> Create(Action render, IntPtr windowHandle)
        {
            var dxVersion = await Utility.GetDXVersion().ConfigureAwait(false);
            return Create(render, windowHandle, dxVersion);
        }

        /// <summary>
        /// Creates a new ImGui hook.
        /// </summary>
        /// <param name="render">Renders your imgui UI</param>
        /// <param name="windowHandle">Handle to the window to render on. Pass IntPtr.Zero to select main window.</param>
        /// <param name="version">DirectX version to handle.</param>
        public static ImguiHook Create(Action render, IntPtr windowHandle, Direct3DVersion version)
        {
            var hook = new ImguiHook(render, windowHandle);

            if (Utility.IsD3D11(version))
            {
                hook.Implementation = new ImguiHookDX11(hook);
            }
            else if (Utility.IsD3D9(version))
            {
                hook.Implementation = new ImguiHookDX9(hook);
            }
            else
            {
                hook.Disable();
                throw new Exception("Unsupported or not found DirectX version.");
            }
            
            return hook;
        }

        public void Enable()
        {
            WndProcHook?.Enable();
            Implementation?.Enable();
        }

        public void Disable()
        {
            WndProcHook?.Disable();
            Implementation?.Disable();
        }
    }
}
