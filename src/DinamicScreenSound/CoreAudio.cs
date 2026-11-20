using System.Runtime.InteropServices;

namespace DinamicScreenSound;

internal sealed record AudioEndpoint(string Id, string Name, bool IsDefault);

internal static class CoreAudio
{
    private const int Render = 0;
    private const int ConsoleRole = 0;
    private const uint Active = 1;
    private const int NotFound = unchecked((int)0x80070490);

    internal static IReadOnlyList<AudioEndpoint> ListOutputs()
    {
        var enumerator = (IDeviceEnumerator)new DeviceEnumerator();
        try
        {
            string? defaultId = GetDefaultOutputId(enumerator);
            enumerator.EnumAudioEndpoints(Render, Active, out IDeviceCollection collection);
            try
            {
                collection.GetCount(out uint count);
                var endpoints = new List<AudioEndpoint>();
                for (uint index = 0; index < count; index++)
                {
                    collection.Item(index, out IDevice device);
                    try
                    {
                        device.GetId(out string id);
                        endpoints.Add(new AudioEndpoint(id, GetFriendlyName(device) ?? id, id == defaultId));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }

                return endpoints;
            }
            finally
            {
                Marshal.ReleaseComObject(collection);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    private static string? GetDefaultOutputId(IDeviceEnumerator enumerator)
    {
        int result = enumerator.GetDefaultAudioEndpoint(Render, ConsoleRole, out IDevice device);
        if (result == NotFound)
        {
            return null;
        }

        Marshal.ThrowExceptionForHR(result);
        try
        {
            device.GetId(out string id);
            return id;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static string? GetFriendlyName(IDevice device)
    {
        var key = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
        device.OpenPropertyStore(0, out IPropertyStore properties);
        try
        {
            properties.GetValue(in key, out PropVariant value);
            try
            {
                const ushort wideString = 31;
                return value.Type == wideString ? Marshal.PtrToStringUni(value.Pointer) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(properties);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant variant);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        private ushort reserved1;
        private ushort reserved2;
        private ushort reserved3;
        public IntPtr Pointer;
        private IntPtr unionPadding;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, uint stateMask, out IDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint index, out IDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice
    {
        void Activate(in Guid interfaceId, uint classContext, IntPtr activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        void OpenPropertyStore(uint access, out IPropertyStore properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(in PropertyKey key, out PropVariant value);
    }
}
