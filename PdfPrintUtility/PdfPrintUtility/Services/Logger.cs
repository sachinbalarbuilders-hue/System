using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPrintUtility.Services
{
    public class LogEntry
    {
        public string DateTime { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Printer { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
    }

    public static class Logger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfPrintUtility", "Logs");

        public static void LogPrint(string fileName, string printerName, string status, string errorDetails = "")
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }

                string logFile = Path.Combine(LogFolder, $"PrintLog_{DateTime.Now:yyyy_MM}.csv");
                bool isNewFile = !File.Exists(logFile);

                using var writer = new StreamWriter(logFile, append: true);
                if (isNewFile)
                {
                    writer.WriteLine("Date Time,Username,Printer,FileName,Status,Error Details");
                }

                string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string user = Environment.UserName;

                // Escape commas in filenames or errors
                fileName = $"\"{fileName.Replace("\"", "\"\"")}\"";
                errorDetails = $"\"{errorDetails.Replace("\"", "\"\"")}\"";

                writer.WriteLine($"{date},{user},{printerName},{fileName},{status},{errorDetails}");
            }
            catch
            {
                // Suppress logger errors to not interrupt the printing process
            }
        }

        public static List<LogEntry> GetHistory()
        {
            var history = new List<LogEntry>();
            try
            {
                if (!Directory.Exists(LogFolder)) return history;

                var files = Directory.GetFiles(LogFolder, "PrintLog_*.csv").OrderByDescending(f => f);
                foreach (var file in files)
                {
                    var lines = File.ReadAllLines(file).Skip(1); // skip header
                    foreach (var line in lines)
                    {
                        var parts = ParseCsvLine(line);
                        if (parts.Count >= 6)
                        {
                            history.Add(new LogEntry
                            {
                                DateTime = parts[0],
                                Username = parts[1],
                                Printer = parts[2],
                                FileName = parts[3],
                                Status = parts[4],
                                ErrorDetails = parts[5]
                            });
                        }
                    }
                }
            }
            catch { }
            return history;
        }

        public static void ClearHistory()
        {
            try
            {
                if (Directory.Exists(LogFolder))
                {
                    foreach (var file in Directory.GetFiles(LogFolder, "PrintLog_*.csv"))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string currentVal = "";
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\"')
                {
                    if (i < line.Length - 1 && line[i + 1] == '\"')
                    {
                        currentVal += "\"";
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (line[i] == ',' && !inQuotes)
                {
                    result.Add(currentVal);
                    currentVal = "";
                }
                else
                {
                    currentVal += line[i];
                }
            }
            result.Add(currentVal);
            return result;
        }
    }
}
