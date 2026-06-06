using System.IO;
using System.Text.Json;

namespace PdfPrintUtility.Models
{
    public class PrintSettings
    {
        public string DefaultPrinter { get; set; } = string.Empty;
        public short Copies { get; set; } = 1;
        public bool Duplex { get; set; } = false;
        public bool Collate { get; set; } = true;
        public string ColorMode { get; set; } = "Color"; // Color, Grayscale
        public string Orientation { get; set; } = "Auto"; // Auto, Portrait, Landscape
        public string PaperSize { get; set; } = "A4"; // A4, Letter, Legal
    }

    public static class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfPrintUtility");
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");

        public static PrintSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<PrintSettings>(json);
                    return settings ?? new PrintSettings();
                }
            }
            catch
            {
                // Ignore parsing errors and return default
            }
            return new PrintSettings();
        }

        public static void Save(PrintSettings settings)
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
    }
}
