using System.Text.Json;

namespace DinamicScreenSound;

internal static class OutputSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DinamicScreenSound", "outputs.json");

    internal static Dictionary<string, string?> Load()
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(SettingsPath));
            return new Dictionary<string, string?>(saved ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static void Save(Dictionary<string, string?> outputs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string pending = SettingsPath + ".tmp";
        File.WriteAllText(pending, JsonSerializer.Serialize(outputs, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(pending, SettingsPath, overwrite: true);
    }
}
