using System.Text.Json;

namespace DinamicScreenSound;

internal static class OutputSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinamicScreenSound");

    internal static Dictionary<string, string?> Load() => Read("outputs.json");

    internal static Dictionary<string, string?> LoadExceptions() => Read("exceptions.json");

    internal static void Save(Dictionary<string, string?> outputs) => Write("outputs.json", outputs);

    internal static void SaveExceptions(Dictionary<string, string?> exceptions) => Write("exceptions.json", exceptions);

    private static Dictionary<string, string?> Read(string filename)
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(SettingsDirectory, filename)));
            return new Dictionary<string, string?>(saved ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Write(string filename, Dictionary<string, string?> outputs)
    {
        Directory.CreateDirectory(SettingsDirectory);
        string path = Path.Combine(SettingsDirectory, filename);
        string pending = path + ".tmp";
        File.WriteAllText(pending, JsonSerializer.Serialize(outputs, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(pending, path, overwrite: true);
    }
}
