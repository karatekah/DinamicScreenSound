using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class DesktopRouter : IDisposable
{
    private readonly Dictionary<string, AppRoute> routes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> previousDisplays = new(StringComparer.OrdinalIgnoreCase);
    private AudioPolicy? policy;

    internal void ResetSamples() => previousDisplays.Clear();

    internal bool Update(IReadOnlyDictionary<string, string?> assignments)
    {
        var paths = new Dictionary<int, string?>();
        string? GetPath(int pid)
        {
            if (!paths.TryGetValue(pid, out string? path))
            {
                using Process? process = OpenProcess(pid);
                path = process is null ? null : ReadPath(process);
                paths.Add(pid, path);
            }

            return path;
        }

        var sessions = CoreAudio.ListActiveSessions()
            .Select(session => (Pid: (int)session.ProcessId, Path: GetPath((int)session.ProcessId)))
            .Where(session => session.Path is not null)
            .ToLookup(session => session.Path!, session => session.Pid, StringComparer.OrdinalIgnoreCase);
        var windows = DesktopWindows.List()
            .Select(window => (Window: window, Path: GetPath(window.ProcessId)))
            .Where(entry => entry.Path is not null && sessions.Contains(entry.Path))
            .ToLookup(entry => entry.Path!, entry => entry.Window.DisplayName, StringComparer.OrdinalIgnoreCase);
        var displays = DisplayCatalog.GetDisplays().ToDictionary(display => display.DeviceName, StringComparer.OrdinalIgnoreCase);
        var available = CoreAudio.ListOutputs().Select(output => output.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var samples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool complete = true;
        foreach (var session in sessions)
        {
            string path = session.Key;
            string? displayName = GetSharedDisplay(windows[path]);
            if (displayName is null || !displays.TryGetValue(displayName, out DesktopDisplay? display) ||
                !assignments.TryGetValue(display.Key, out string? output))
            {
                continue;
            }

            samples[path] = display.Key;
            if (!previousDisplays.TryGetValue(path, out string? previous) || previous != display.Key)
            {
                continue;
            }

            string? target = output is not null && available.Contains(output) ? output : null;
            try
            {
                Apply(path, session.First(), target);
            }
            catch (Exception error) when (error is COMException or Win32Exception)
            {
                complete = false;
            }
        }

        previousDisplays = samples;
        return complete;
    }

    internal static string? GetSharedDisplay(IEnumerable<string?> windows)
    {
        string? display = null;
        foreach (string? window in windows)
        {
            if (window is null || (display is not null && !string.Equals(display, window, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            display = window;
        }

        return display;
    }

    private void Apply(string path, int pid, string? target)
    {
        routes.TryGetValue(path, out AppRoute? route);
        if (route is not null)
        {
            route.AudioPid = pid;
            if (route.ManuallyChanged || (route.Applied && route.EndpointId == target))
            {
                return;
            }
        }

        using Process? process = OpenProcess(pid);
        if (process is null || !string.Equals(ReadPath(process), path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        policy ??= new AudioPolicy();
        if (route is null)
        {
            route = new AppRoute(new TemporaryOutput(policy, process), pid);
            routes.Add(path, route);
        }

        try
        {
            route.Output.Apply(process, target);
            route.EndpointId = target;
            route.Applied = true;
        }
        catch (OutputChangedException)
        {
            route.ManuallyChanged = true;
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The session ended between enumeration and applying its output.
        }
    }

    internal int Restore()
    {
        int failures = 0;
        foreach ((string path, AppRoute route) in routes)
        {
            if (!route.Output.HasChanges)
            {
                continue;
            }

            using Process? process = FindProcess(path, route.AudioPid);
            if (process is null)
            {
                failures++;
                continue;
            }

            try
            {
                route.Output.Restore(process);
                if (route.Output.HasChanges)
                {
                    failures++;
                }
            }
            catch (Exception error) when (error is COMException or Win32Exception or InvalidOperationException)
            {
                failures++;
            }
        }

        routes.Clear();
        return failures;
    }

    private static Process? FindProcess(string path, int pid)
    {
        Process? process = OpenProcess(pid);
        if (process is not null && string.Equals(ReadPath(process), path, StringComparison.OrdinalIgnoreCase))
        {
            return process;
        }

        process?.Dispose();
        Process[] candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path));
        Process? match = candidates.FirstOrDefault(candidate => string.Equals(ReadPath(candidate), path, StringComparison.OrdinalIgnoreCase));
        foreach (Process candidate in candidates)
        {
            if (candidate != match)
            {
                candidate.Dispose();
            }
        }

        return match;
    }

    private static Process? OpenProcess(int pid)
    {
        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public void Dispose() => policy?.Dispose();

    private sealed class AppRoute(TemporaryOutput output, int pid)
    {
        internal TemporaryOutput Output { get; } = output;
        internal int AudioPid { get; set; } = pid;
        internal string? EndpointId { get; set; }
        internal bool Applied { get; set; }
        internal bool ManuallyChanged { get; set; }
    }
}
