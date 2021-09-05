namespace Reloaded.Imgui.Hook.Direct3D9.Definitions
{
    /// <summary>
    /// Contains the D3D9 interface.
    /// </summary>
    public enum IDirect3D9
    {
        /*** IUnknown methods ***/
        QueryInterface,
        AddRef,
        Release,

        /*** IDirect3D9 methods ***/
        RegisterSoftwareDevice,
        GetAdapterCount,
        GetAdapterIdentifier,
        GetAdapterModeCount,
        EnumAdapterModes,
        GetAdapterDisplayMode,
        CheckDeviceType,
        CheckDeviceFormat,
        CheckDeviceMultiSampleType,
        CheckDepthStencilMatch,
        CheckDeviceFormatConversion,
        GetDeviceCaps,
        GetAdapterMonitor,
        CreateDevice
    }
}
