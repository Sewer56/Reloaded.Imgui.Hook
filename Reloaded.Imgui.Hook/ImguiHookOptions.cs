using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reloaded.Imgui.Hook
{
    public class ImguiHookOptions
    {
        /// <summary>
        /// [Experimental! + Initialisation Only!]
        /// Enables the viewports feature of Dear ImGui; which puts individual ImGui windows onto invisible windows, allowing
        /// them to escape the program region/area.
        /// </summary>
        public bool EnableViewports = false;

        /// <summary>
        /// [Real Time]
        /// Tries to suppress window deactivation message to the application/game.
        /// Sometimes necessary to stop the application from pausing when <see cref="EnableViewports"/> is turned on.
        /// </summary>
        public bool IgnoreWindowUnactivate = false;
    }
}
