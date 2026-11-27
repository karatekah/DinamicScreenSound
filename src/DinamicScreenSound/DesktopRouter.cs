using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class DesktopRouter : IDisposable
{
    private readonly Dictionary<string, AppRoute> routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WindowIdentity> references = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> previousDisplays = new(StringComparer.OrdinalIgnoreCase);
    private AudioPolicy? policy;

    internal void ResetSamples() => previousDisplays.Clear();

    internal IReadOnlyDictionary<string, WindowIdentity> References => references;

    internal void SetReference(string path, WindowIdentity? window)
    {
        if (window is { } identity)
        {
            if (DesktopWindows.Exists(identity))
            {
                references[path] = identity;
            }
        }
        else
        {
            references.Remove(path);
        }

        previousDisplays.Remove(path);
    }

    internal (bool Complete, bool SharedAcrossDisplays) Update(IReadOnlyDictionary<string, string?> assignments,
        IReadOnlyDictionary<string, string?> exceptions)
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
            .ToLookup(entry => entry.Path!, entry => entry.Window, StringComparer.OrdinalIgnoreCase);
        var displays = DisplayCatalog.GetDisplays().ToDictionary(display => display.DeviceName, StringComparer.OrdinalIgnoreCase);
        var available = CoreAudio.ListOutputs().Select(output => output.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var samples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool complete = true;
        bool sharedAcrossDisplays = false;
        foreach (var (path, window) in references)
        {
            if (!DesktopWindows.Exists(window))
            {
                references.Remove(path);
                previousDisplays.Remove(path);
            }
        }

        foreach (var session in sessions)
        {
            string path = session.Key;
            WindowIdentity? reference = references.TryGetValue(path, out WindowIdentity window) ? window : null;
            IEnumerable<string?> windowDisplays = GetRoutingDisplays(windows[path], reference);
            OutputTarget? target = ResolveOutput(path, windowDisplays, displays, assignments, exceptions, available);
            if (target is null)
            {
                sharedAcrossDisplays |= HasConflictingDisplays(windowDisplays, displays, assignments);
                continue;
            }

            if (target.Value.DisplayKey is { } key)
            {
                samples[path] = key;
                if (!previousDisplays.TryGetValue(path, out string? previous) || previous != key)
                {
                    continue;
                }
            }

            try
            {
                Apply(path, session.First(), target.Value.EndpointId);
            }
            catch (Exception error) when (error is COMException or Win32Exception)
            {
                complete = false;
            }
        }

        previousDisplays = samples;
        return (complete, sharedAcrossDisplays);
    }

    internal static IEnumerable<string?> GetRoutingDisplays(IEnumerable<DesktopWindow> windows, WindowIdentity? reference)
    {
        if (reference is { } identity)
        {
            // Preserve the output while the selected window has no stable display.
            return [windows.FirstOrDefault(window => window.Identity == identity)?.DisplayName];
        }

        return windows.Select(window => window.DisplayName);
    }

    internal static bool HasConflictingDisplays(IEnumerable<string?> windows,
        IReadOnlyDictionary<string, DesktopDisplay> displays, IReadOnlyDictionary<string, string?> assignments)
    {
        string[] names = windows.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return names.Length > 1 && names.Any(name =>
            displays.TryGetValue(name, out DesktopDisplay? display) && assignments.ContainsKey(display.Key));
    }

    internal static OutputTarget? ResolveOutput(string path, IEnumerable<string?> windows,
        IReadOnlyDictionary<string, DesktopDisplay> displays, IReadOnlyDictionary<string, string?> assignments,
        IReadOnlyDictionary<string, string?> exceptions, IReadOnlySet<string> available)
    {
        string? displayKey = null;
        if (!exceptions.TryGetValue(path, out string? output))
        {
            string? displayName = GetSharedDisplay(windows);
            if (displayName is null || !displays.TryGetValue(displayName, out DesktopDisplay? display) ||
                !assignments.TryGetValue(display.Key, out output))
            {
                return null;
            }

            displayKey = display.Key;
        }

        return new OutputTarget(output is not null && available.Contains(output) ? output : null, displayKey);
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

    internal static Process? OpenProcess(int pid)
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

    internal static string? ReadPath(Process process)
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

    internal readonly record struct OutputTarget(string? EndpointId, string? DisplayKey);

    private sealed class AppRoute(TemporaryOutput output, int pid)
    {
        internal TemporaryOutput Output { get; } = output;
        internal int AudioPid { get; set; } = pid;
        internal string? EndpointId { get; set; }
        internal bool Applied { get; set; }
        internal bool ManuallyChanged { get; set; }
    }
}
