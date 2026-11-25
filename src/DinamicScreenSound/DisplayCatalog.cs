using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed record DesktopDisplay(string Key, string DeviceName, string Label);

internal static class DisplayCatalog
{
    internal static DesktopDisplay[] GetDisplays() => Screen.AllScreens
        .OrderByDescending(screen => screen.Primary)
        .ThenBy(screen => screen.Bounds.X)
        .ThenBy(screen => screen.Bounds.Y)
        .Select(GetDisplay)
        .ToArray();

    private static DesktopDisplay GetDisplay(Screen screen)
    {
        const uint includeDeviceInterface = 1;
        const uint activeMonitor = 1;
        var monitors = new List<DisplayDevice>();
        for (uint index = 0; ; index++)
        {
            var device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevicesW(screen.DeviceName, index, ref device, includeDeviceInterface))
            {
                break;
            }

            if ((device.StateFlags & activeMonitor) != 0 && !string.IsNullOrWhiteSpace(device.Id))
            {
                monitors.Add(device);
            }
        }

        string key = monitors.Count == 0
            ? screen.DeviceName
            : string.Join("|", monitors.Select(monitor => monitor.Id).Order(StringComparer.OrdinalIgnoreCase));
        string number = screen.DeviceName.Replace(@"\\.\DISPLAY", "Display ", StringComparison.OrdinalIgnoreCase);
        string primary = screen.Primary ? " (primary)" : "";
        string position = $"({screen.Bounds.X}, {screen.Bounds.Y})";
        return new DesktopDisplay(key, screen.DeviceName, $"{number}{primary} {position}");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevicesW(string adapter, uint index, ref DisplayDevice device, uint flags);
}
