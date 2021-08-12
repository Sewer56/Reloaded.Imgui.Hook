using System;

namespace Reloaded.Imgui.Hook.DirectX.Definitions
{
    /// <summary>
    /// The Direct3DVersion enumerable is used often as a parameter
    /// to state which version of DirectX is to be used for hooking,
    /// rendering among other various operations within Reloaded Mod Loader.
    /// </summary>
    [Flags]
    public enum Direct3DVersion : int
    {
        /// <summary>
        /// Unknown or not a DirectX process
        /// </summary>
        Null,

        /// <summary>
        /// DirectX 9
        /// </summary>
        Direct3D9 = 0b0000_0001,

        /// <summary>
        /// [Currently Unsupported]
        /// DirectX 10
        /// </summary>
        Direct3D10 = 0b0000_0010,

        /// <summary>
        /// [Currently Unsupported]
        /// DirectX 10.1
        /// </summary>
        Direct3D10_1 = 0b0000_0100,

        /// <summary>
        /// DirectX 11
        /// </summary>
        Direct3D11 = 0b0000_1000,

        /// <summary>
        /// DirectX 11.1
        /// </summary>
        Direct3D11_1 = 0b0001_0000,

        /// <summary>
        /// DirectX 11.2
        /// </summary>
        Direct3D11_2 = 0b0010_0000,

        /// <summary>
        /// DirectX 11.3
        /// </summary>
        Direct3D11_3 = 0b0100_0000,

        /// <summary>
        /// DirectX 11.4
        /// </summary>
        Direct3D11_4 = 0b1000_0000,
    }
}