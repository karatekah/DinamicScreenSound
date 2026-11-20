using System.Runtime.InteropServices;
using System.Text;

namespace DinamicScreenSound;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length != 0 && !(args.Length == 1 && args[0] == "list"))
        {
            Console.Error.WriteLine("Usage: DinamicScreenSound [list]");
            return 2;
        }

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
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
        catch (COMException error)
        {
            Console.Error.WriteLine($"Windows could not query audio devices (0x{error.HResult:X8}): {error.Message}");
            return 1;
        }
    }
}
