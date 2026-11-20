using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed record AudioSession(uint ProcessId, string EndpointId, float Peak);

internal static partial class CoreAudio
{
    internal static IReadOnlyList<AudioSession> ListActiveSessions()
    {
        var sessions = new List<AudioSession>();
        var enumerator = (IDeviceEnumerator)new DeviceEnumerator();
        try
        {
            foreach (IDevice device in EnumerateOutputs(enumerator))
            {
                device.GetId(out string endpointId);
                Guid managerId = typeof(ISessionManager).GUID;
                const uint inProcessServer = 1;
                device.Activate(in managerId, inProcessServer, IntPtr.Zero, out object instance);
                try
                {
                    var manager = (ISessionManager)instance;
                    manager.GetSessionEnumerator(out ISessionEnumerator sessionEnumerator);
                    try
                    {
                        ReadSessions(sessionEnumerator, endpointId, sessions);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sessionEnumerator);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(instance);
                }
            }

            return sessions;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static void ReadSessions(ISessionEnumerator enumerator, string endpointId, List<AudioSession> sessions)
    {
        enumerator.GetCount(out int count);
        for (int index = 0; index < count; index++)
        {
            enumerator.GetSession(index, out object instance);
            try
            {
                var session = (ISessionControl)instance;
                session.GetState(out int state);
                const int activeSession = 1;
                if (state != activeSession || session.GetProcessId(out uint processId) != 0 || processId == 0)
                {
                    continue;
                }

                var meter = (IAudioMeter)instance;
                meter.GetPeakValue(out float peak);
                sessions.Add(new AudioSession(processId, endpointId, peak));
            }
            finally
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionManager
    {
        void GetAudioSessionControl(in Guid sessionId, uint flags, out IntPtr sessionControl);
        void GetSimpleAudioVolume(in Guid sessionId, uint flags, out IntPtr volume);
        void GetSessionEnumerator(out ISessionEnumerator enumerator);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionControl
    {
        void GetState(out int state);
        void GetDisplayName(out IntPtr name);
        void SetDisplayName(IntPtr name, in Guid eventContext);
        void GetIconPath(out IntPtr path);
        void SetIconPath(IntPtr path, in Guid eventContext);
        void GetGroupingParam(out Guid groupingId);
        void SetGroupingParam(in Guid groupingId, in Guid eventContext);
        void RegisterAudioSessionNotification(IntPtr notification);
        void UnregisterAudioSessionNotification(IntPtr notification);
        void GetSessionIdentifier(out IntPtr identifier);
        void GetSessionInstanceIdentifier(out IntPtr identifier);

        [PreserveSig]
        int GetProcessId(out uint processId);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeter
    {
        void GetPeakValue(out float peak);
    }
}
