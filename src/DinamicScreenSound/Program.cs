using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DinamicScreenSound;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            return args switch
            {
                [] or ["list"] => ListDevices(),
                ["sessions"] => ListSessions(),
                ["probe", var process, var output] when int.TryParse(process, out int pid) && pid > 0 =>
                    AudioProbe.Run(pid, output),
                ["--help"] => ShowUsage(0),
                _ => ShowUsage(2)
            };
        }
        catch (COMException error)
        {
            Console.Error.WriteLine($"Windows could not complete the audio operation (0x{error.HResult:X8}): {error.Message}");
            return 1;
        }
    }

    private static int ListDevices()
    {
        Console.WriteLine("Displays");
        foreach (Screen screen in Screen.AllScreens)
        {
            var bounds = screen.Bounds;
            string primary = screen.Primary ? " · primary" : "";
            Console.WriteLine($"  {screen.DeviceName} · {bounds.Width}×{bounds.Height} at ({bounds.X}, {bounds.Y}){primary}");
        }

        Console.WriteLine();
        Console.WriteLine("Active audio outputs");
        foreach (AudioEndpoint endpoint in CoreAudio.ListOutputs())
        {
            string defaultLabel = endpoint.IsDefault ? " · default" : "";
            Console.WriteLine($"  {endpoint.Name}{defaultLabel}");
        }

        return 0;
    }

    private static int ListSessions()
    {
        Dictionary<string, AudioEndpoint> outputs = CoreAudio.ListOutputs().ToDictionary(output => output.Id);
        IReadOnlyList<AudioSession> sessions = CoreAudio.ListActiveSessions();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No active audio sessions.");
        }

        foreach (AudioSession session in sessions)
        {
            string output = outputs.GetValueOrDefault(session.EndpointId)?.Name ?? session.EndpointId;
            Console.WriteLine($"  PID {session.ProcessId} ({ProcessName(session.ProcessId)}) · {output} · peak {session.Peak:F3}");
        }

        return 0;
    }

    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "exited";
        }
        catch (InvalidOperationException)
        {
            return "exited";
        }
    }

    private static int ShowUsage(int exitCode)
    {
        Console.WriteLine("Usage: DinamicScreenSound [list | sessions | probe <audio-pid> <output-name>]");
        return exitCode;
    }
}
