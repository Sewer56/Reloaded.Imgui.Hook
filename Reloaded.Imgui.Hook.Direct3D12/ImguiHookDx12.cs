using DearImguiSharp;

using Reloaded.Hooks.Definitions;
using Reloaded.Imgui.Hook;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook.Misc;
using Reloaded.Imgui.Hook.Direct3D12.Definitions;

using SharpDX.Direct3D12;
using SharpDX.DXGI;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static Reloaded.Imgui.Hook.Misc.Native;

using Device = SharpDX.Direct3D12.Device;

namespace Reloaded.Imgui.Hook.Direct3D12;

public unsafe class ImguiHookDx12 : IImguiHook
{
    public static ImguiHookDx12 Instance { get; private set; }

    private IHook<DX12Hook.Present> _presentHook;
    private IHook<DX12Hook.ResizeBuffers> _resizeBuffersHook;
    //private IHook<DX12Hook.ExecuteCommandLists> _execCmdListHook;
    private bool _initialized = false;
    private DescriptorHeap shaderResourceViewDescHeap;
    private DescriptorHeap renderTargetViewDescHeap;
    private List<FrameContext> _frameContexts = [];
    private GraphicsCommandList _commandList;
    private CommandQueue _commandQueue;

    private static readonly string[] _supportedDlls = new string[]
    {
        "d3d12.dll",
    };

    /*
     * In some cases (E.g. under DX9 + Viewports enabled), Dear ImGui might call
     * DirectX functions from within its internal logic.
     *
     * We put a lock on the current thread in order to prevent stack overflow.
     */
    private bool _presentRecursionLock = false;
    private bool _resizeRecursionLock = false;

    public ImguiHookDx12() { }

    public bool IsApiSupported()
    {
        foreach (var dll in _supportedDlls)
        {
            if (GetModuleHandle(dll) != IntPtr.Zero)
                return true;
        }

        // Fallback to detecting D3D12Core
        if (File.Exists(Path.Combine("D3D12", "D3D12Core.dll")))
            return true;

        return false;
    }

    public void Initialize()
    {
        var presentPtr = (long)DX12Hook.SwapchainVTable[(int)IDXGISwapChainVTable.Present].FunctionPointer;
        var resizeBuffersPtr = (long)DX12Hook.SwapchainVTable[(int)IDXGISwapChainVTable.ResizeBuffers].FunctionPointer;
        //var executeCommandListsPtr = (long)DX12Hook.ComamndQueueVTable[(int)ID3D12CommandQueueVTable.ExecuteCommandLists].FunctionPointer;
        Instance = this;
        _presentHook = SDK.Hooks.CreateHook<DX12Hook.Present>(typeof(ImguiHookDx12), nameof(PresentImplStatic), presentPtr).Activate();
        _resizeBuffersHook = SDK.Hooks.CreateHook<DX12Hook.ResizeBuffers>(typeof(ImguiHookDx12), nameof(ResizeBuffersImplStatic), resizeBuffersPtr).Activate();
        //_execCmdListHook = SDK.Hooks.CreateHook<DX12Hook.ExecuteCommandLists>(typeof(ImguiHookDx12), nameof(ExecCmdListsImplStatic), executeCommandListsPtr).Activate();
    }

    ~ImguiHookDx12()
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private void ReleaseUnmanagedResources()
    {
        if (_initialized)
        {
            Debug.WriteLine($"[DX12 Dispose] Shutdown");
            ImGui.ImGuiImplDX12Shutdown();
        }
    }

    private IntPtr ResizeBuffersImpl(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags)
    {
        if (_resizeRecursionLock)
        {
            Debug.WriteLine($"[DX12 ResizeBuffers] Discarding via Recursion Lock");
            return _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
        }

        if (!_initialized || renderTargetViewDescHeap is null) // Our device was probably not yet created, fine to just reroute to original
            return _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);

        _resizeRecursionLock = true;
        try
        {
            // Dispose all frame context resources
            PreResizeBuffers();
            var result = _resizeBuffersHook.OriginalFunction.Value.Invoke(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);
            if (result != IntPtr.Zero)
            {
                Debug.DebugWriteLine($"[DX12 ResizeBuffers] ResizeBuffers original failed with {result:X}");
                return result;
            }

            PostResizeBuffers(swapchainPtr);
            return result;
        }
        finally
        {
            _resizeRecursionLock = false;
        }
    }

    private void PreResizeBuffers()
    {
        // ResizeBuffer requires swapchain resources to be freed.
        foreach (var frameCtx in _frameContexts)
            frameCtx.MainRenderTargetResource?.Dispose();
        ImGui.ImGuiImplDX12InvalidateDeviceObjects();
    }

    private Device PostResizeBuffers(nint swapchainPtr)
    {
        var swapChain = new SwapChain(swapchainPtr);
        using var device = swapChain.GetDevice<Device>();

        var windowHandle = swapChain.Description.OutputHandle;
        Debug.DebugWriteLine($"[DX12 ResizeBuffers] Window Handle {windowHandle:X}");

        var rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        var rtvHandle = renderTargetViewDescHeap.CPUDescriptorHandleForHeapStart;

        for (var i = 0; i < _frameContexts.Count; i++)
        {
            _frameContexts[i].main_render_target_descriptor = rtvHandle;
            var resource = swapChain.GetBackBuffer<SharpDX.Direct3D12.Resource>(i);
            device.CreateRenderTargetView(resource, null, rtvHandle);
            _frameContexts[i].MainRenderTargetResource = resource;
            rtvHandle.Ptr += rtvDescriptorSize;
        }

        ImGui.ImGuiImplDX12CreateDeviceObjects();
        return device;
    }

    // Not currently used. Refer to queue comment in PresentImpl.
    /*
    private void ExecCmdListOverride(IntPtr pQueue, uint NumCommandLists, IntPtr ppCommandLists)
    {
        var queue = new CommandQueue(pQueue);
        if (g_pD3DCommandQueue == null && queue.Description.Type == CommandListType.Direct)
        {
            // Hijacking the game's command queue.
            g_pD3DCommandQueue = queue;
            _execCmdListHook.Disable();
        }
        _execCmdListHook.OriginalFunction.Value.Invoke(pQueue, NumCommandLists, ppCommandLists);
    }
    */

    private unsafe nint PresentImpl(nint swapChainPtr, int syncInterval, PresentFlags flags)
    {
        if (_presentRecursionLock)
        {
            Debug.WriteLine($"[DX12 Present] Discarding via Recursion Lock");
            return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
        }

        _presentRecursionLock = true;
        try
        {
            var swapChain = new SwapChain3(swapChainPtr);
            var windowHandle = swapChain.Description.OutputHandle;

            // Ignore windows which don't belong to us.
            if (!ImguiHook.CheckWindowHandle(windowHandle))
            {
                Debug.WriteLine($"[DX12 Present] Discarding Window Handle {windowHandle:X} due to Mismatch");
                return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
            }

            using var device = swapChain.GetDevice<Device>();
            if (device is null)
                return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);

            var frameBufferCount = swapChain.Description.BufferCount;
            if (!_initialized)
            {
                var descriptorImGuiRender = new DescriptorHeapDescription
                {
                    Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                    DescriptorCount = frameBufferCount,
                    Flags = DescriptorHeapFlags.ShaderVisible
                };
                shaderResourceViewDescHeap = device.CreateDescriptorHeap(descriptorImGuiRender);
                if (shaderResourceViewDescHeap == null)
                {
                    Debug.WriteLine($"[DX12 Present] Failed to create shader resource view descriptor heap.");
                    return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
                }

                Debug.WriteLine($"[DX12 Present] Init DX12, Window Handle: {windowHandle:X}");

                var renderTargetDesc = new DescriptorHeapDescription
                {
                    Type = DescriptorHeapType.RenderTargetView,
                    DescriptorCount = frameBufferCount,
                    Flags = DescriptorHeapFlags.None,
                    NodeMask = 1
                };
                renderTargetViewDescHeap = device.CreateDescriptorHeap(renderTargetDesc);
                if (renderTargetViewDescHeap == null)
                    return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);

                var rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
                var rtvHandle = renderTargetViewDescHeap.CPUDescriptorHandleForHeapStart;

                for (var i = 0; i < frameBufferCount; i++)
                {
                    _frameContexts.Add(new FrameContext
                    {
                        main_render_target_descriptor = rtvHandle,
                        MainRenderTargetResource = swapChain.GetBackBuffer<SharpDX.Direct3D12.Resource>(i),
                    });
                    device.CreateRenderTargetView(_frameContexts[i].MainRenderTargetResource, null, rtvHandle);
                    rtvHandle.Ptr += rtvDescriptorSize;
                }
                
                // Create command list
                for (var i = 0; i < frameBufferCount; i++)
                {
                    _frameContexts[i].CommandAllocator = device.CreateCommandAllocator(CommandListType.Direct);
                    if (_frameContexts[i].CommandAllocator == null)
                        return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
                }

                _commandList = device.CreateCommandList(0, CommandListType.Direct, _frameContexts[0].CommandAllocator, null);
                if (_commandList == null)
                    return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
                _commandList.Close();

                ImguiHook.InitializeWithHandle(windowHandle);
                ImGui.ImGuiImplDX12Init((void*)device.NativePointer, frameBufferCount, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8UNORM,
                    new ID3D12DescriptorHeap((void*)shaderResourceViewDescHeap.NativePointer),
                    shaderResourceViewDescHeap.CPUDescriptorHandleForHeapStart.Ptr,
                    shaderResourceViewDescHeap.GPUDescriptorHandleForHeapStart.Ptr);

                _initialized = true;
            }


            ImGui.ImGuiImplDX12NewFrame();
            ImguiHook.NewFrame();

            var FrameBufferCountsfgn = swapChain.Description.BufferCount;
            var currentFrameContext = _frameContexts[swapChain.CurrentBackBufferIndex];
            currentFrameContext.CommandAllocator.Reset();

            var barrier = new ResourceBarrier
            {
                Type = ResourceBarrierType.Transition,
                Flags = ResourceBarrierFlags.None,
                Transition = new ResourceTransitionBarrier(currentFrameContext.MainRenderTargetResource, -1, ResourceStates.Present, ResourceStates.RenderTarget)
            };
            _commandList.Reset(currentFrameContext.CommandAllocator, null);
            _commandList.ResourceBarrier(barrier);
            _commandList.SetRenderTargets(currentFrameContext.main_render_target_descriptor, null);
            _commandList.SetDescriptorHeaps(shaderResourceViewDescHeap);

            ImGui.ImGuiImplDX12RenderDrawData(ImGui.GetDrawData(), new ID3D12GraphicsCommandList((void*)_commandList.NativePointer));
            barrier.Transition = new ResourceTransitionBarrier(currentFrameContext.MainRenderTargetResource, -1, ResourceStates.RenderTarget, ResourceStates.Present);
            _commandList.ResourceBarrier(barrier);
            _commandList.Close();

            // Normally we would hook ExecuteCommandList to grab the command queue, but it causes the following issue in certain games (S.T.A.L.K.E.R Enhanced Edition):
            // "D3D12 ERROR: ID3D12CommandQueue::ExecuteCommandLists:
            // A command list, which writes to a swap chain back buffer, may only be executed on the command queue associated with that buffer.
            // [ STATE_SETTING ERROR #907: EXECUTECOMMANDLISTS_WRONGSWAPCHAINBUFFERREFERENCE]"
            
            // Grabbing the queue pointer from the swapchain (offset is scanned in DX12Hook.cs) seems to be OK.
            // https://stackoverflow.com/questions/36286425/how-do-i-get-the-directx-12-command-queue-from-a-swap-chain

            var queuePtr = *(nint*)(swapChain.NativePointer + DX12Hook.CommandQueueOffset!.Value);
            if (queuePtr != 0)
            {
                var cmdQueue = new CommandQueue(queuePtr);
                cmdQueue.ExecuteCommandList(_commandList);
            }

            return _presentHook.OriginalFunction.Value.Invoke(swapChainPtr, syncInterval, flags);
        }
        finally
        {
            _presentRecursionLock = false;
        }
    }

    public void Disable()
    {
        _presentHook?.Disable();
        _resizeBuffersHook?.Disable();
        //_execCmdListHook?.Disable();
    }

    public void Enable()
    {
        _presentHook?.Enable();
        _resizeBuffersHook?.Enable();
        //_execCmdListHook?.Enable();
    }

    #region Hook Functions
    //[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    //private static void ExecCmdListsImplStatic(IntPtr pQueue, uint NumCommandLists, IntPtr ppCommandLists) => Instance.ExecCmdListOverride(pQueue, NumCommandLists, ppCommandLists);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static IntPtr ResizeBuffersImplStatic(IntPtr swapchainPtr, uint bufferCount, uint width, uint height, Format newFormat, uint swapchainFlags) => Instance.ResizeBuffersImpl(swapchainPtr, bufferCount, width, height, newFormat, swapchainFlags);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint PresentImplStatic(nint swapChainPtr, int syncInterval, PresentFlags flags) => Instance.PresentImpl(swapChainPtr, syncInterval, flags);
    #endregion
}

class FrameContext
{
    public CommandAllocator CommandAllocator;
    public SharpDX.Direct3D12.Resource MainRenderTargetResource;
    public CpuDescriptorHandle main_render_target_descriptor;
};