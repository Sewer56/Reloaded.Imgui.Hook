using System.Diagnostics;

namespace Reloaded.Imgui.Hook.Misc
{
    public class Debug
    {
        [Conditional("DEBUG")]
        public static void DebugWriteLine(string text) => SDK.Debug?.Invoke(text);
        public static void WriteLine(string text) => SDK.Debug?.Invoke(text);
    }
}
