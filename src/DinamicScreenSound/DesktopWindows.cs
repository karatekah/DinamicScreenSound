using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed record DesktopWindow(int ProcessId, string? DisplayName);

internal static class DesktopWindows
{
    internal static List<DesktopWindow> List()
    {
        var windows = new List<DesktopWindow>();
        EnumWindows((handle, _) =>
        {
            const uint owner = 4;
            const uint cloakedAttribute = 14;
            if (!IsWindowVisible(handle) || GetWindowTextLengthW(handle) == 0 || GetWindow(handle, owner) != 0 ||
                (DwmGetWindowAttribute(handle, cloakedAttribute, out uint cloaked, sizeof(uint)) == 0 && cloaked != 0))
            {
                return true;
            }

            uint thread = GetWindowThreadProcessId(handle, out int processId);
            if (thread != 0 && processId != Environment.ProcessId)
            {
                windows.Add(new DesktopWindow(processId, WindowFollower.SampleScreen(handle, thread)));
            }

            return true;
        }, 0);
        return windows;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool EnumWindowCallback(nint window, nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowTextLengthW(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(nint window, uint attribute, out uint value, uint size);
}
