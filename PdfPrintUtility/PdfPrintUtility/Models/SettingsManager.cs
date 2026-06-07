using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PdfPrintUtility.Models
{
    public class PrintSettings
    {
        public string ProfileName { get; set; } = "Default";
        public string DefaultPrinter { get; set; } = string.Empty;
        public short Copies { get; set; } = 1;
        public bool Duplex { get; set; } = false;
        public bool Collate { get; set; } = true;
        public string ColorMode { get; set; } = "Color"; // Color, Grayscale
        public string Orientation { get; set; } = "Auto"; // Auto, Portrait, Landscape
        public string PaperSize { get; set; } = "A4"; // A4, Letter, Legal
        public string WatermarkText { get; set; } = string.Empty;
        public string FitMode { get; set; } = "FitToPage"; // FitToPage, ShrinkToFit, ActualSize
        public string PaperTray { get; set; } = string.Empty; // Empty = Auto (printer default)
    }

    public class SettingsData
    {
        public List<PrintSettings> Profiles { get; set; } = new List<PrintSettings>();
        public string LastUsedProfile { get; set; } = "Default";
    }

    public static class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfPrintUtility");
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings_v2.json");

        public static SettingsData LoadData()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null && data.Profiles.Count > 0)
                    {
                        return data;
                    }
                }
                
                // Fallback to v1 settings
                string v1File = Path.Combine(AppDataFolder, "settings.json");
                if (File.Exists(v1File))
                {
                    var json = File.ReadAllText(v1File);
                    var oldSettings = JsonSerializer.Deserialize<PrintSettings>(json);
                    if (oldSettings != null)
                    {
                        oldSettings.ProfileName = "Default";
                        var data = new SettingsData { LastUsedProfile = "Default" };
                        data.Profiles.Add(oldSettings);
                        return data;
                    }
                }
            }
            catch { }
            
            var newData = new SettingsData { LastUsedProfile = "Default" };
            newData.Profiles.Add(new PrintSettings { ProfileName = "Default" });
            return newData;
        }

        public static void SaveData(SettingsData data)
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
    }
}
