using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using static Reloaded.Imgui.Hook.Misc.Native;
using Debug = Reloaded.Imgui.Hook.Misc.Debug;

namespace Reloaded.Imgui.Hook.DirectX
{
    internal static class Utility
    {
        /// <summary>
        /// Gets the DirectX version loaded into the current process.
        /// </summary>
        /// <param name="retryTime">Time between retries in milliseconds.</param>
        /// <param name="timeout">Timeout in milliseconds to determine DX version.</param>
        public static async Task<Direct3DVersion> GetDXVersion(int retryTime = 64, int timeout = 20000)
        {
            // Store the amount of attempts taken at hooking DirectX for a process.
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // Loop until DirectX module found.
            var versions = Direct3DVersion.Null;
            while (true)
            {
                if ((long)GetModuleHandle("d3d9.dll") != 0) { versions |= Direct3DVersion.Direct3D9; }
                if ((long)GetModuleHandle("d3d10.dll") != 0) { versions |= Direct3DVersion.Direct3D10; }
                if ((long)GetModuleHandle("d3d10_1.dll") != 0) { versions |= Direct3DVersion.Direct3D10_1; }
                if ((long)GetModuleHandle("d3d11.dll") != 0) { versions |= Direct3DVersion.Direct3D11; }
                if ((long)GetModuleHandle("d3d11_1.dll") != 0) { versions |= Direct3DVersion.Direct3D11_1; }
                if ((long)GetModuleHandle("d3d11_2.dll") != 0) { versions |= Direct3DVersion.Direct3D11_2; }
                if ((long)GetModuleHandle("d3d11_3.dll") != 0) { versions |= Direct3DVersion.Direct3D11_3; }
                if ((long)GetModuleHandle("d3d11_4.dll") != 0) { versions |= Direct3DVersion.Direct3D11_4; }

                // Check timeout.
                if (stopWatch.ElapsedMilliseconds > timeout)
                    throw new Exception("DirectX module not found, the application is either not a DirectX application or uses an unsupported version of DirectX.");

                // Check every X milliseconds.
                if (versions != Direct3DVersion.Null)
                {
                    Debug.WriteLine($"DirectX Versions Detected: {versions}");
                    return versions;
                }


                await Task.Delay(retryTime).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// True if the returned version is D3D11.
        /// </summary>#
        public static bool IsD3D11(Direct3DVersion version)
        {
            var d3d11flags = Direct3DVersion.Direct3D11 | Direct3DVersion.Direct3D11_1 |
                             Direct3DVersion.Direct3D11_2 | Direct3DVersion.Direct3D11_3 |
                             Direct3DVersion.Direct3D11_4;

            return (version & d3d11flags) > 0;
        }

        /// <summary>
        /// True if the returned version is D3D11.
        /// </summary>#
        public static bool IsD3D9(Direct3DVersion version) => version.HasFlag(Direct3DVersion.Direct3D9);
    }
}
