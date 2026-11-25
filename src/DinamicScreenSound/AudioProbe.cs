using System.Diagnostics;

namespace DinamicScreenSound;

internal static class AudioProbe
{
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(3);

    internal static int Run(int pid, string outputName)
    {
        var outputs = CoreAudio.ListOutputs();
        var matches = outputs.Where(output => string.Equals(output.Name, outputName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
        {
            Console.Error.WriteLine("Enter the exact name of one active output from list.");
            return 2;
        }

        AudioSession[] sourceSessions = CoreAudio.ListActiveSessions().Where(session => session.ProcessId == pid).ToArray();
        string[] sources = sourceSessions.Select(session => session.EndpointId).Distinct().ToArray();
        AudioEndpoint target = matches[0];
        if (sources.Length != 1 || sources[0] == target.Id)
        {
            Console.Error.WriteLine("The PID must have an active session on one output other than the target.");
            return 2;
        }

        using Process process = Process.GetProcessById(pid);
        var originalOutput = outputs.FirstOrDefault(output => output.Id == sources[0]);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, args) =>
        {
            args.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            using var policy = new AudioPolicy();
            var route = new TemporaryOutput(policy, process);
            var timer = new Stopwatch();
            TimeSpan? outbound;
            try
            {
                Console.WriteLine($"PID {pid} · {originalOutput?.Name ?? sources[0]} → {target.Name}");
                timer.Start();
                route.Apply(process, target.Id);
                outbound = WaitForSignal((uint)pid, target.Id, timer, cancellation.Token);
                Report("To target", outbound);
                if (outbound.HasValue)
                {
                    cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }
            }
            finally
            {
                timer.Restart();
                route.Restore(process);
            }

            if (!cancellation.IsCancellationRequested && outbound.HasValue)
            {
                TimeSpan? inbound = WaitForSignal((uint)pid, sources[0], timer, cancellation.Token);
                Report("Back to original", inbound);
                return inbound.HasValue ? 0 : 1;
            }

            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    private static TimeSpan? WaitForSignal(uint pid, string endpointId, Stopwatch timer, CancellationToken cancellation)
    {
        while (timer.Elapsed < ObservationWindow && !cancellation.IsCancellationRequested)
        {
            if (CoreAudio.ListActiveSessions().Any(session => session.ProcessId == pid && session.EndpointId == endpointId && session.Peak > 0))
            {
                return timer.Elapsed;
            }

            cancellation.WaitHandle.WaitOne(40);
        }

        return null;
    }

    private static void Report(string direction, TimeSpan? elapsed)
    {
        Console.WriteLine(elapsed.HasValue
            ? $"{direction}: signal detected in {elapsed.Value.TotalMilliseconds:F0} ms."
            : $"{direction}: no signal detected on the expected output.");
    }
}
