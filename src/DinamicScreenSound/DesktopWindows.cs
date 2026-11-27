using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal readonly record struct WindowIdentity(nint Handle, uint ThreadId, int ProcessId);
internal sealed record DesktopWindow(WindowIdentity Identity, string? DisplayName, string Title)
{
    internal int ProcessId => Identity.ProcessId;
}

internal static class DesktopWindows
{
    internal static List<DesktopWindow> List(bool includeTitles = false)
    {
        var windows = new List<DesktopWindow>();
        EnumWindows((handle, _) =>
        {
            const uint owner = 4;
            const uint cloakedAttribute = 14;
            if (!IsWindowVisible(handle) || GetWindow(handle, owner) != 0 ||
                (DwmGetWindowAttribute(handle, cloakedAttribute, out uint cloaked, sizeof(uint)) == 0 && cloaked != 0))
            {
                return true;
            }

            int titleLength = GetWindowTextLengthW(handle);
            uint thread = GetWindowThreadProcessId(handle, out int processId);
            if (titleLength > 0 && thread != 0 && processId != Environment.ProcessId)
            {
                string title = "";
                if (includeTitles)
                {
                    var buffer = new System.Text.StringBuilder(titleLength + 1);
                    GetWindowTextW(handle, buffer, buffer.Capacity);
                    title = buffer.ToString();
                }

                windows.Add(new DesktopWindow(new WindowIdentity(handle, thread, processId),
                    WindowFollower.SampleScreen(handle, thread), title));
            }

            return true;
        }, 0);
        return windows;
    }

    internal static bool Exists(WindowIdentity window) =>
        GetWindowThreadProcessId(window.Handle, out int pid) == window.ThreadId && pid == window.ProcessId;

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

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint window, System.Text.StringBuilder text, int capacity);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(nint window, uint attribute, out uint value, uint size);
}
