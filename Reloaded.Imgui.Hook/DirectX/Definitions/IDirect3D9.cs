using System;
using System.Collections.Generic;
using System.Text;

namespace Reloaded.Imgui.Hook.DirectX.Definitions
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
