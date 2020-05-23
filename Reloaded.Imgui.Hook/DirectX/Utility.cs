using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Reloaded.Imgui.Hook.DirectX.Definitions;
using static Reloaded.Imgui.Hook.Misc.Native;

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
            while (true)
            {
                if ((long)GetModuleHandle("d3d9.dll") != 0) { return Direct3DVersion.Direct3D9; }
                if ((long)GetModuleHandle("d3d10.dll") != 0) { return Direct3DVersion.Direct3D10; }
                if ((long)GetModuleHandle("d3d10_1.dll") != 0) { return Direct3DVersion.Direct3D10_1; }
                if ((long)GetModuleHandle("d3d11.dll") != 0) { return Direct3DVersion.Direct3D11; }
                if ((long)GetModuleHandle("d3d11_1.dll") != 0) { return Direct3DVersion.Direct3D11_1; }
                if ((long)GetModuleHandle("d3d11_2.dll") != 0) { return Direct3DVersion.Direct3D11_2; }
                if ((long)GetModuleHandle("d3d11_3.dll") != 0) { return Direct3DVersion.Direct3D11_3; }
                if ((long)GetModuleHandle("d3d11_4.dll") != 0) { return Direct3DVersion.Direct3D11_4; }

                // Check timeout.
                if (stopWatch.ElapsedMilliseconds > timeout)
                    throw new Exception("DirectX module not found, the application is either not a DirectX application or uses an unsupported version of DirectX.");

                // Check every X milliseconds.
                await Task.Delay(retryTime);
            }
        }

        /// <summary>
        /// True if the returned version is D3D11.
        /// </summary>#
        public static bool IsD3D11(Direct3DVersion version)
        {
            return version == Direct3DVersion.Direct3D11 || version == Direct3DVersion.Direct3D10_1
                                                         || version == Direct3DVersion.Direct3D11_2
                                                         || version == Direct3DVersion.Direct3D11_3
                                                         || version == Direct3DVersion.Direct3D11_4;
        }

        /// <summary>
        /// True if the returned version is D3D11.
        /// </summary>#
        public static bool IsD3D9(Direct3DVersion version)
        {
            return version == Direct3DVersion.Direct3D9;
        }
    }
}
