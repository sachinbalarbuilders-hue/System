using System;
using System.IO;

namespace PdfPrintUtility.Services
{
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
    }
}
