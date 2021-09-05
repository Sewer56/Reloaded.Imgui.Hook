namespace Reloaded.Imgui.Hook.DirectX.Definitions
{
    /// <summary>
    /// Contains a full list of IDXGISwapChain functions to be used alongside
    /// <see cref="DX11Hook"/> as an indexer into the SwapChain Virtual Function Table entries.
    /// </summary>
    public enum IDXGISwapChain
    {
        // IUnknown
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,

        // IDXGIObject
        SetPrivateData = 3,
        SetPrivateDataInterface = 4,
        GetPrivateData = 5,
        GetParent = 6,

        // IDXGIDeviceSubObject
        GetDevice = 7,

        // IDXGISwapChain
        Present = 8,
        GetBuffer = 9,
        SetFullscreenState = 10,
        GetFullscreenState = 11,
        GetDesc = 12,
        ResizeBuffers = 13,
        ResizeTarget = 14,
        GetContainingOutput = 15,
        GetFrameStatistics = 16,
        GetLastPresentCount = 17,
    }
}
