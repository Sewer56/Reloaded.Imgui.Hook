using DearImguiSharp;

using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Structs;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Imgui.Hook.Direct3D12.Definitions;

using SharpDX.Direct3D;
using SharpDX.Direct3D12;
using SharpDX.DXGI;

using System.Diagnostics;

using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;

namespace Reloaded.Imgui.Hook.Direct3D12;

/// <summary>
/// Provides access to DirectX 11 functions.
/// </summary>
public static class DX12Hook
{
    /// <summary>
    /// Contains the DX12 DXGI Swapchain VTable.
    /// </summary>
    public static IVirtualFunctionTable SwapchainVTable { get; private set; }

    /// <summary>
    /// Contains the DX12 DXGI Command Queue VTable.
    /// </summary>
    public static IVirtualFunctionTable ComamndQueueVTable { get; private set; }

    static DX12Hook()
    {
        // Uncomment this if you need debug logs with DebugView.
        // DebugInterface.Get().EnableDebugLayer();

        // Define
        var device = new SharpDX.Direct3D12.Device(null, SharpDX.Direct3D.FeatureLevel.Level_12_0);
        CommandQueue commandQueue = device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
        var swapChainDesc = new SwapChainDescription()
        {
            BufferCount = 2,
            ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            Usage = Usage.RenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard,
            OutputHandle = Process.GetCurrentProcess().MainWindowHandle,
            //Flags = SwapChainFlags.None,
            SampleDescription = new SampleDescription(1, 0),
            IsWindowed = true
        };

        using (var factory = new Factory4())
        using (var swapChain = new SwapChain(factory, commandQueue, swapChainDesc))
        {
            SwapchainVTable = SDK.Hooks.VirtualFunctionTableFromObject(swapChain.NativePointer, Enum.GetNames(typeof(IDXGISwapChainVTable)).Length);
            ComamndQueueVTable = SDK.Hooks.VirtualFunctionTableFromObject(commandQueue.NativePointer, Enum.GetNames(typeof(ID3D12CommandQueueVTable)).Length);
        }

        // Cleanup
        device.Dispose();
    }

    [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
    [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
    public struct Present { public FuncPtr<IntPtr, int, PresentFlags, IntPtr> Value; }

    [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
    [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
    public struct ResizeBuffers { public FuncPtr<IntPtr, uint, uint, uint, Format, uint, IntPtr> Value; }

    [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
    [Reloaded.Hooks.Definitions.X86.Function(CallingConventions.Stdcall)]
    public struct ExecuteCommandLists { public FuncPtr<IntPtr, uint, IntPtr, IntPtr> Value; }
}