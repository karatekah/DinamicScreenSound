using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed class AudioPolicy : IDisposable
{
    private const int Render = 0;
    private const string DevicePathPrefix = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterfaceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    private readonly IntPtr factory;
    private readonly GetEndpoint getEndpoint;
    private readonly SetEndpoint setEndpoint;

    internal AudioPolicy()
    {
        Marshal.ThrowExceptionForHR(RoInitialize(0));
        try
        {
            const string className = "Windows.Media.Internal.AudioPolicyConfig";
            Marshal.ThrowExceptionForHR(WindowsCreateString(className, (uint)className.Length, out IntPtr classId));
            try
            {
                var interfaceId = new Guid("AB3D4648-E242-459F-B02F-541C70306324");
                Marshal.ThrowExceptionForHR(RoGetActivationFactory(classId, in interfaceId, out factory));
            }
            finally
            {
                WindowsDeleteString(classId);
            }

            // Windows 11 audio-policy ABI: IInspectable followed by 19 methods before the setter.
            IntPtr table = Marshal.ReadIntPtr(factory);
            setEndpoint = Marshal.GetDelegateForFunctionPointer<SetEndpoint>(Marshal.ReadIntPtr(table, 25 * IntPtr.Size));
            getEndpoint = Marshal.GetDelegateForFunctionPointer<GetEndpoint>(Marshal.ReadIntPtr(table, 26 * IntPtr.Size));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal string? GetPreference(uint processId, int role)
    {
        int result = getEndpoint(factory, processId, Render, role, out IntPtr devicePath);
        try
        {
            const int notFound = unchecked((int)0x80070490);
            const int fileNotFound = unchecked((int)0x80070002);
            if (result is notFound or fileNotFound)
            {
                return null;
            }

            Marshal.ThrowExceptionForHR(result);
            IntPtr text = WindowsGetStringRawBuffer(devicePath, out uint length);
            return length == 0 ? null : Marshal.PtrToStringUni(text, checked((int)length));
        }
        finally
        {
            WindowsDeleteString(devicePath);
        }
    }

    internal static string DevicePath(string endpointId) => DevicePathPrefix + endpointId + RenderInterfaceSuffix;

    internal void SetPreference(uint processId, int role, string? devicePath)
    {
        IntPtr endpoint = IntPtr.Zero;
        if (devicePath is not null)
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(devicePath, (uint)devicePath.Length, out endpoint));
        }

        try
        {
            Marshal.ThrowExceptionForHR(setEndpoint(factory, processId, Render, role, endpoint));
        }
        finally
        {
            WindowsDeleteString(endpoint);
        }
    }

    public void Dispose()
    {
        if (factory != IntPtr.Zero)
        {
            Marshal.Release(factory);
        }

        RoUninitialize();
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetEndpoint(IntPtr self, uint processId, int flow, int role, out IntPtr endpoint);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetEndpoint(IntPtr self, uint processId, int flow, int role, IntPtr endpoint);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoInitialize(int apartmentType);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern void RoUninitialize();

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr classId, in Guid interfaceId, out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string text, uint length, out IntPtr result);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr text);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr text, out uint length);
}
