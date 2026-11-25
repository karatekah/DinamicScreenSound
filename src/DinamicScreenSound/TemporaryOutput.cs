using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class TemporaryOutput
{
    private readonly AudioPolicy policy;
    private readonly (int Role, string? Original)[] previous;
    private readonly Dictionary<int, string?> changed = new();

    internal bool HasChanges => changed.Count != 0;

    internal TemporaryOutput(AudioPolicy policy, Process process)
    {
        this.policy = policy;
        const int consoleRole = 0;
        const int multimediaRole = 1;
        uint pid = (uint)process.Id;
        previous =
        [
            (consoleRole, policy.GetPreference(pid, consoleRole)),
            (multimediaRole, policy.GetPreference(pid, multimediaRole))
        ];
    }

    internal void Apply(Process process, string? endpointId)
    {
        string? target = endpointId is null ? null : AudioPolicy.DevicePath(endpointId);
        uint pid = (uint)process.Id;
        foreach (var (role, original) in previous)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("The application closed during the test.");
            }

            string? current = policy.GetPreference(pid, role);
            string? expected = changed.TryGetValue(role, out string? lastApplied) ? lastApplied : original;
            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new OutputChangedException();
            }

            if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            policy.SetPreference(pid, role, target);
            if (string.Equals(target, original, StringComparison.OrdinalIgnoreCase))
            {
                changed.Remove(role);
            }
            else
            {
                changed[role] = target;
            }
        }
    }

    internal void Restore(Process process)
    {
        uint pid = (uint)process.Id;
        COMException? failure = null;
        foreach (var (role, original) in previous)
        {
            if (!changed.TryGetValue(role, out string? expected))
            {
                continue;
            }

            try
            {
                if (process.HasExited)
                {
                    Console.Error.WriteLine("The application closed before its audio output could be restored.");
                    return;
                }

                if (string.Equals(policy.GetPreference(pid, role), expected, StringComparison.OrdinalIgnoreCase))
                {
                    policy.SetPreference(pid, role, original);
                }

                changed.Remove(role);
            }
            catch (COMException error)
            {
                failure ??= error;
            }
        }

        if (failure is not null)
        {
            throw new COMException("Some audio output settings could not be restored.", failure.HResult);
        }
    }
}

internal sealed class OutputChangedException() : InvalidOperationException(
    "The output was changed in Windows. Tracking stopped to preserve that setting.");
