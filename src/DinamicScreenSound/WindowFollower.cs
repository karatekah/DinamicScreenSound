using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal static class WindowFollower
{
    private static readonly TimeSpan OutputCheckInterval = TimeSpan.FromMilliseconds(500);

    internal static int Run(int windowPid, int audioPid, string primaryName, string secondaryName)
    {
        using Process windowProcess = Process.GetProcessById(windowPid);
        using Process audioProcess = Process.GetProcessById(audioPid);
        string? windowPath = windowProcess.MainModule?.FileName;
        nint window = windowProcess.MainWindowHandle;
        if (window == 0 || windowPath is null ||
            !string.Equals(windowPath, audioProcess.MainModule?.FileName, StringComparison.OrdinalIgnoreCase) ||
            !CoreAudio.ListActiveSessions().Any(session => session.ProcessId == audioPid))
        {
            Console.Error.WriteLine("Select a window and an active audio session from the same executable.");
            return 2;
        }

        Screen[] screens = Screen.AllScreens;
        var outputs = CoreAudio.ListOutputs();
        AudioEndpoint[] primary = outputs.Where(output => string.Equals(output.Name, primaryName, StringComparison.OrdinalIgnoreCase)).ToArray();
        AudioEndpoint[] secondary = outputs.Where(output => string.Equals(output.Name, secondaryName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (screens.Length != 2 || primary.Length != 1 || secondary.Length != 1 || primary[0] == secondary[0])
        {
            Console.Error.WriteLine("The prototype requires two displays and two distinct audio outputs. Run list to find the output names.");
            return 2;
        }

        var routes = screens.ToDictionary(screen => screen.DeviceName, screen => screen.Primary ? primary[0] : secondary[0]);
        uint windowThread = GetWindowThreadProcessId(window, out _);
        using var policy = new AudioPolicy();
        var route = new TemporaryOutput(policy, audioProcess);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, args) =>
        {
            args.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        Task<string?> stop = Task.Run(Console.ReadLine);
        try
        {
            Console.WriteLine("Tracking the window. Press Enter or Ctrl+C to exit and restore the previous audio output.");
            Follow(window, windowThread, audioProcess, routes, route, stop, cancellation.Token);
            return 0;
        }
        finally
        {
            try
            {
                route.Restore(audioProcess);
            }
            finally
            {
                Console.CancelKeyPress -= cancel;
            }
        }
    }

    private static void Follow(nint window, uint windowThread, Process audioProcess, Dictionary<string, AudioEndpoint> routes,
        TemporaryOutput route, Task<string?> stop, CancellationToken cancellation)
    {
        string? previousSample = null;
        AudioEndpoint? applied = null;
        bool wasOnDefault = false;
        long lastOutputCheck = 0;
        while (!cancellation.IsCancellationRequested && !(stop.IsCompletedSuccessfully && stop.Result is not null) &&
               IsWindow(window) && !audioProcess.HasExited)
        {
            string? sample = SampleScreen(window, windowThread);
            if (sample is not null && sample == previousSample && routes.TryGetValue(sample, out AudioEndpoint? endpoint) &&
                (endpoint != applied || Stopwatch.GetElapsedTime(lastOutputCheck) >= OutputCheckInterval))
            {
                lastOutputCheck = Stopwatch.GetTimestamp();
                IReadOnlyList<AudioEndpoint> outputs = CoreAudio.ListOutputs();
                bool useDefault = !outputs.Any(output => output.Id == endpoint.Id);
                if (endpoint != applied || useDefault != wasOnDefault)
                {
                    route.Apply(audioProcess, useDefault ? null : endpoint.Id);
                    Console.WriteLine($"{sample} → {(useDefault ? "Windows default output" : endpoint.Name)}");
                    applied = endpoint;
                    wasOnDefault = useDefault;
                }
            }

            previousSample = sample;
            cancellation.WaitHandle.WaitOne(100);
        }
    }

    internal static string? SampleScreen(nint window, uint thread)
    {
        const uint inMoveOrSizeLoop = 0x00000002;
        var gui = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        if (thread == 0 || IsIconic(window) || !GetGUIThreadInfo(thread, ref gui) || (gui.Flags & inMoveOrSizeLoop) != 0)
        {
            return null;
        }

        return Screen.FromHandle(window).DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size;
        public uint Flags;
        private nint active;
        private nint focus;
        private nint capture;
        private nint menuOwner;
        private nint moveSize;
        private nint caret;
        private Rectangle caretBounds;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
}
