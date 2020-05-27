using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DearImguiSharp;
using Reloaded.Imgui.Hook.DirectX;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using Reloaded.Imgui.Hook.Implementations;

namespace Reloaded.Imgui.Hook
{
    public class ImguiHook : IDisposable
    {
        /// <summary>
        /// User supplied function to render the imgui UI.
        /// </summary>
        public Action Render { get; private set; }

        private WndProcHook _wndProcHook;
        private IImguiHook _implementation;
        private ImGuiContext _context;
        private IntPtr _windowHandle;
        private bool _initialized;

        // Construction/Destruction
        private ImguiHook(Action render, IntPtr windowHandle)
        {
            Render = render;
            _windowHandle = windowHandle;
            _context = ImGui.CreateContext(null);
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
                _implementation?.Dispose();
        }

        private void ReleaseUnmanagedResources()
        {
            ImGui.ImGuiImplWin32Shutdown();
            ImGui.DestroyContext(_context);
        }

        /// <summary>
        /// Hooks WndProc to allow for input for ImGui
        /// </summary>
        private unsafe IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (ImGui.ImplWin32_WndProcHandler((void*)hWnd, msg, wParam, lParam) != IntPtr.Zero)
                return new IntPtr(1);

            return _wndProcHook.Hook.OriginalFunction(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Called from renderer implementation, renders a new frame.
        /// </summary>
        internal void NewFrame()
        {
            if (!_initialized)
            {
                if (_windowHandle == IntPtr.Zero)
                    _windowHandle = _implementation.GetWindowHandle();

                if (_windowHandle == IntPtr.Zero)
                    return;

                ImGui.ImGuiImplWin32Init(_windowHandle); 
                _wndProcHook = new WndProcHook(_windowHandle, WndProcHandler);
                _initialized = true;
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
            var dxVersion = await Utility.GetDXVersion();
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
            var dxVersion = await Utility.GetDXVersion();
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
            var hook       = new ImguiHook(render, windowHandle);

            if (Utility.IsD3D11(version))
            {
                hook._implementation = new ImguiHookDX11(hook);
            }
            else if (Utility.IsD3D9(version))
            {
                hook._implementation = new ImguiHookDX9(hook);
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
            _wndProcHook?.Enable();
            _implementation?.Enable();
        }

        public void Disable()
        {
            _wndProcHook?.Disable();
            _implementation?.Disable();
        }
    }
}
