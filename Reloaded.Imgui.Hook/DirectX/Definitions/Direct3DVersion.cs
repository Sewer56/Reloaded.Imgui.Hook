namespace Reloaded.Imgui.Hook.DirectX.Definitions
{
    /// <summary>
    /// The Direct3DVersion enumerable is used often as a parameter
    /// to state which version of DirectX is to be used for hooking,
    /// rendering among other various operations within Reloaded Mod Loader.
    /// </summary>
    public enum Direct3DVersion
    {
        /// <summary>
        /// Unknown or not a DirectX process
        /// </summary>
        Null,

        /// <summary>
        /// DirectX 9
        /// </summary>
        Direct3D9,

        /// <summary>
        /// [Currently Unsupported]
        /// DirectX 10
        /// </summary>
        Direct3D10,

        /// <summary>
        /// [Currently Unsupported]
        /// DirectX 10.1
        /// </summary>
        Direct3D10_1,

        /// <summary>
        /// DirectX 11
        /// </summary>
        Direct3D11,

        /// <summary>
        /// DirectX 11.1
        /// </summary>
        Direct3D11_1,

        /// <summary>
        /// DirectX 11.2
        /// </summary>
        Direct3D11_2,

        /// <summary>
        /// DirectX 11.3
        /// </summary>
        Direct3D11_3,

        /// <summary>
        /// DirectX 11.4
        /// </summary>
        Direct3D11_4
    }
}