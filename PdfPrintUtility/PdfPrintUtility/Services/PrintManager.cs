using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Printing;
using PdfiumViewer;
using PdfPrintUtility.Models;

namespace PdfPrintUtility.Services
{
    public static class PrintManager
    {
        // Supported extensions
        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" };
        public static readonly string[] WordExtensions  = { ".doc", ".docx" };

        // ─── Route to correct printer based on file type ───
        public static bool PrintFile(string filePath, PrintSettings settings, string pageRange, out string errorMessage)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".pdf")                                    return PrintPdf(filePath, settings, pageRange, out errorMessage);
            if (Array.Exists(ImageExtensions, e => e == ext))    return PrintImage(filePath, settings, out errorMessage);
            if (Array.Exists(WordExtensions,  e => e == ext))    return PrintWord(filePath, settings, out errorMessage);
            errorMessage = $"Unsupported file type: {ext}";
            return false;
        }

        // ─── Image printing ───
        public static bool PrintImage(string filePath, PrintSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                string targetPrinter = string.IsNullOrWhiteSpace(settings.DefaultPrinter)
                    ? new PrinterSettings().PrinterName : settings.DefaultPrinter;

                if (!IsPrinterReady(targetPrinter, out errorMessage)) return false;

                using var image = Image.FromFile(filePath);

                using var printDoc = new PrintDocument();
                printDoc.PrinterSettings.PrinterName = targetPrinter;
                printDoc.PrintController = new StandardPrintController();
                printDoc.DefaultPageSettings.Color = settings.ColorMode == "Color";

                // Orientation: auto = match image aspect
                bool landscape = settings.Orientation == "Landscape" ||
                                 (settings.Orientation == "Auto" && image.Width > image.Height);
                printDoc.DefaultPageSettings.Landscape = landscape;

                // Apply paper size robustly
                string actualPaperSize = settings.PaperSize;
                if (actualPaperSize == "Auto (Match Document)")
                {
                    float imgNativeW = image.Width * 72f / image.HorizontalResolution;
                    float imgNativeH = image.Height * 72f / image.VerticalResolution;
                    float maxDim = Math.Max(imgNativeW, imgNativeH);
                    actualPaperSize = maxDim > 850 ? "A3" : "A4";
                }

                if (!string.IsNullOrWhiteSpace(actualPaperSize) && actualPaperSize != "Auto")
                {
                    PaperKind targetKind = actualPaperSize switch
                    {
                        "A3" => PaperKind.A3,
                        "A4" => PaperKind.A4,
                        "Letter" => PaperKind.Letter,
                        "Legal" => PaperKind.Legal,
                        _ => PaperKind.Custom
                    };

                    PaperSize? bestMatch = null;
                    foreach (PaperSize sz in printDoc.PrinterSettings.PaperSizes)
                    {
                        if (targetKind != PaperKind.Custom && sz.Kind == targetKind) { bestMatch = sz; break; }
                        if (sz.PaperName.Equals(actualPaperSize, StringComparison.OrdinalIgnoreCase)) { bestMatch = sz; }
                        else if (bestMatch == null && sz.PaperName.Contains(actualPaperSize, StringComparison.OrdinalIgnoreCase)) { bestMatch = sz; }
                    }
                    if (bestMatch != null) printDoc.DefaultPageSettings.PaperSize = bestMatch;
                }

                // Apply tray robustly
                if (string.IsNullOrWhiteSpace(settings.PaperTray) || settings.PaperTray == "Auto" || settings.PaperTray == "Auto (Printer Default)")
                {
                    foreach (PaperSource src in printDoc.PrinterSettings.PaperSources)
                    {
                        if (src.Kind == PaperSourceKind.AutomaticFeed ||
                            src.SourceName.Equals("Auto Select", StringComparison.OrdinalIgnoreCase) ||
                            src.SourceName.Equals("Automatically Select", StringComparison.OrdinalIgnoreCase))
                        {
                            printDoc.DefaultPageSettings.PaperSource = src;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (PaperSource src in printDoc.PrinterSettings.PaperSources)
                    {
                        if (src.SourceName.Equals(settings.PaperTray, StringComparison.OrdinalIgnoreCase) ||
                            src.SourceName.Contains(settings.PaperTray, StringComparison.OrdinalIgnoreCase))
                        { 
                            printDoc.DefaultPageSettings.PaperSource = src; 
                            break; 
                        }
                    }
                }
                // Convert to grayscale in-memory if needed
                Bitmap bmp;
                if (settings.ColorMode == "Grayscale")
                {
                    bmp = new Bitmap(image.Width, image.Height);
                    var cm = new ColorMatrix(new float[][] {
                        new[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                        new[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                        new[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                        new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } });
                    var attrs = new ImageAttributes();
                    attrs.SetColorMatrix(cm);
                    using var g = Graphics.FromImage(bmp);
                    g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height),
                        0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attrs);
                }
                else bmp = new Bitmap(image);

                printDoc.PrintPage += (s, e) =>
                {
                    var bounds = e.MarginBounds;
                    float sx = (float)bounds.Width / bmp.Width;
                    float sy = (float)bounds.Height / bmp.Height;
                    float scale = settings.FitMode == "ActualSize"   ? 1f
                                : settings.FitMode == "ShrinkToFit" ? Math.Min(1f, Math.Min(sx, sy))
                                : Math.Min(sx, sy); // FitToPage
                    int dw = (int)(bmp.Width * scale), dh = (int)(bmp.Height * scale);
                    int dx = bounds.X + (bounds.Width - dw) / 2;
                    int dy = bounds.Y + (bounds.Height - dh) / 2;
                    e.Graphics!.DrawImage(bmp, dx, dy, dw, dh);

                    // Watermark
                    if (!string.IsNullOrWhiteSpace(settings.WatermarkText))
                    {
                        var st = e.Graphics.Save();
                        e.Graphics.TranslateTransform(e.PageBounds.Width / 2, e.PageBounds.Height / 2);
                        e.Graphics.RotateTransform(-45);
                        var font = new Font("Arial", 72, FontStyle.Bold);
                        var brush = new SolidBrush(Color.FromArgb(80, 255, 0, 0));
                        var sz = e.Graphics.MeasureString(settings.WatermarkText, font);
                        e.Graphics.DrawString(settings.WatermarkText, font, brush, -sz.Width / 2, -sz.Height / 2);
                        e.Graphics.Restore(st);
                    }
                };

                for (int i = 0; i < settings.Copies; i++) printDoc.Print();
                bmp.Dispose();
                return true;
            }
            catch (Exception ex) { errorMessage = ex.Message; return false; }
        }

        // ─── Word printing via shell PrintTo verb (uses Word silently if installed) ───
        public static bool PrintWord(string filePath, PrintSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                string targetPrinter = string.IsNullOrWhiteSpace(settings.DefaultPrinter)
                    ? new PrinterSettings().PrinterName : settings.DefaultPrinter;

                // "PrintTo" verb opens Word silently and prints to the specified printer
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = filePath,
                    Verb      = "PrintTo",
                    Arguments = $"\"{targetPrinter}\"",
                    CreateNoWindow  = true,
                    WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                process?.WaitForExit(60000); // wait up to 60 seconds
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Word print failed: {ex.Message}\n(Ensure Microsoft Word is installed.)";
                return false;
            }
        }

        // ─── PDF printing ───
        public static bool PrintPdf(string filePath, PrintSettings settings, string pageRange, out string errorMessage)
        {

            errorMessage = string.Empty;

            try
            {
                string targetPrinter = string.IsNullOrWhiteSpace(settings.DefaultPrinter) 
                    ? new PrinterSettings().PrinterName // Use system default
                    : settings.DefaultPrinter;

                if (!IsPrinterReady(targetPrinter, out errorMessage))
                {
                    return false;
                }

                using var document = PdfDocument.Load(filePath);
                using var printDocument = document.CreatePrintDocument();

                printDocument.PrinterSettings.PrinterName = targetPrinter;

                printDocument.PrinterSettings.Copies = settings.Copies;
                printDocument.PrinterSettings.Collate = settings.Collate;

                if (settings.Duplex && printDocument.PrinterSettings.CanDuplex)
                {
                    printDocument.PrinterSettings.Duplex = Duplex.Vertical; // Or Horizontal depending on needs, Vertical is standard long-edge
                }
                else
                {
                    printDocument.PrinterSettings.Duplex = Duplex.Simplex;
                }

                // Apply Color Mode
                printDocument.DefaultPageSettings.Color = settings.ColorMode == "Color";

                // Apply Orientation
                if (settings.Orientation == "Portrait")
                {
                    printDocument.DefaultPageSettings.Landscape = false;
                }
                else if (settings.Orientation == "Landscape")
                {
                    printDocument.DefaultPageSettings.Landscape = true;
                }

                // Apply Paper Size robustly
                string actualPaperSize = settings.PaperSize;
                if (actualPaperSize == "Auto (Match Document)")
                {
                    var firstPageSz = document.PageSizes[0];
                    float maxDim = Math.Max((float)firstPageSz.Width, (float)firstPageSz.Height);
                    actualPaperSize = maxDim > 850 ? "A3" : "A4";
                }

                if (!string.IsNullOrWhiteSpace(actualPaperSize) && actualPaperSize != "Auto")
                {
                    PaperKind targetKind = actualPaperSize switch
                    {
                        "A3" => PaperKind.A3,
                        "A4" => PaperKind.A4,
                        "Letter" => PaperKind.Letter,
                        "Legal" => PaperKind.Legal,
                        _ => PaperKind.Custom
                    };

                    PaperSize? bestMatch = null;
                    foreach (PaperSize size in printDocument.PrinterSettings.PaperSizes)
                    {
                        if (targetKind != PaperKind.Custom && size.Kind == targetKind) { bestMatch = size; break; }
                        if (size.PaperName.Equals(actualPaperSize, StringComparison.OrdinalIgnoreCase)) { bestMatch = size; }
                        else if (bestMatch == null && size.PaperName.Contains(actualPaperSize, StringComparison.OrdinalIgnoreCase)) { bestMatch = size; }
                    }
                    if (bestMatch != null) printDocument.DefaultPageSettings.PaperSize = bestMatch;
                }

                // Apply Paper Tray robustly
                if (string.IsNullOrWhiteSpace(settings.PaperTray) || settings.PaperTray == "Auto" || settings.PaperTray == "Auto (Printer Default)")
                {
                    foreach (PaperSource source in printDocument.PrinterSettings.PaperSources)
                    {
                        if (source.Kind == PaperSourceKind.AutomaticFeed ||
                            source.SourceName.Equals("Auto Select", StringComparison.OrdinalIgnoreCase) ||
                            source.SourceName.Equals("Automatically Select", StringComparison.OrdinalIgnoreCase))
                        {
                            printDocument.DefaultPageSettings.PaperSource = source;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (PaperSource source in printDocument.PrinterSettings.PaperSources)
                    {
                        if (source.SourceName.Equals(settings.PaperTray, StringComparison.OrdinalIgnoreCase) ||
                            source.SourceName.Contains(settings.PaperTray, StringComparison.OrdinalIgnoreCase))
                        {
                            printDocument.DefaultPageSettings.PaperSource = source;
                            break;
                        }
                    }
                }

                // Use StandardPrintController to prevent the print dialog/progress dialog from appearing
                printDocument.PrintController = new StandardPrintController();

                if (!string.IsNullOrWhiteSpace(settings.WatermarkText))
                {
                    printDocument.PrintPage += (s, e) =>
                    {
                        var state = e.Graphics.Save();
                        e.Graphics.TranslateTransform(e.PageBounds.Width / 2, e.PageBounds.Height / 2);
                        e.Graphics.RotateTransform(-45);
                        var font = new Font("Arial", 72, FontStyle.Bold);
                        var brush = new SolidBrush(Color.FromArgb(80, 255, 0, 0)); // Semi-transparent red
                        var size = e.Graphics.MeasureString(settings.WatermarkText, font);
                        e.Graphics.DrawString(settings.WatermarkText, font, brush, -size.Width / 2, -size.Height / 2);
                        e.Graphics.Restore(state);
                    };
                }

                var ranges = ParsePageRanges(pageRange, document.PageCount);
                foreach (var range in ranges)
                {
                    printDocument.PrinterSettings.PrintRange = PrintRange.SomePages;
                    printDocument.PrinterSettings.FromPage = range.Start;
                    printDocument.PrinterSettings.ToPage = range.End;

                    // Apply Fit Mode
                    // PdfiumViewer's PdfPrintDocument scales to fit by default (FitToPage).
                    // For ActualSize / ShrinkToFit we override the PrintPage event.
                    if (settings.FitMode == "ActualSize" || settings.FitMode == "ShrinkToFit")
                    {
                        printDocument.PrintPage += (s, pe) =>
                        {
                            if (pe.Graphics == null) return;
                            int pageIdx = printDocument.PrinterSettings.FromPage - 1;
                            var pageSz = document.PageSizes[pageIdx];

                            // Native size in printer units (100ths of an inch)
                            float printerDpi = pe.Graphics.DpiX;
                            float nativeW = pageSz.Width  / 72f * printerDpi;
                            float nativeH = pageSz.Height / 72f * printerDpi;
                            float printW  = pe.PageBounds.Width  * printerDpi / 100f;
                            float printH  = pe.PageBounds.Height * printerDpi / 100f;

                            float scale = 1f;
                            if (settings.FitMode == "ShrinkToFit")
                                scale = Math.Min(1f, Math.Min(printW / nativeW, printH / nativeH));
                            // ActualSize: scale stays 1f (no scaling)

                            // Center on page
                            float x = (printW - nativeW * scale) / 2f;
                            float y = (printH - nativeH * scale) / 2f;
                            pe.Graphics.TranslateTransform(x * 100f / printerDpi, y * 100f / printerDpi);
                            pe.Graphics.ScaleTransform(scale, scale);
                            // Let PdfiumViewer's base draw at natural size
                        };
                    }
                    // FitToPage: PdfiumViewer default behavior — no override needed

                    printDocument.Print();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool IsPrinterReady(string printerName, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var printServer = new LocalPrintServer();
                var queue = printServer.GetPrintQueue(printerName);
                if (queue != null)
                {
                    queue.Refresh();

                    if (queue.IsOffline) { errorMessage = "Printer is offline."; return false; }
                    if (queue.IsOutOfPaper || queue.HasPaperProblem || ((queue.QueueStatus & PrintQueueStatus.PaperOut) == PrintQueueStatus.PaperOut)) 
                    { 
                        errorMessage = "Printer is out of paper."; 
                        return false; 
                    }
                    if (queue.IsPaperJammed || ((queue.QueueStatus & PrintQueueStatus.PaperJam) == PrintQueueStatus.PaperJam)) 
                    { 
                        errorMessage = "Printer has a paper jam."; 
                        return false; 
                    }
                    if (queue.IsDoorOpened || ((queue.QueueStatus & PrintQueueStatus.DoorOpen) == PrintQueueStatus.DoorOpen)) 
                    { 
                        errorMessage = "Printer door is open."; 
                        return false; 
                    }
                    if (((queue.QueueStatus & PrintQueueStatus.NotAvailable) == PrintQueueStatus.NotAvailable)) 
                    { 
                        errorMessage = "Printer is not responding."; 
                        return false; 
                    }
                    if (queue.IsInError || ((queue.QueueStatus & PrintQueueStatus.Error) == PrintQueueStatus.Error)) 
                    { 
                        errorMessage = "Printer is in an error state."; 
                        return false; 
                    }
                }
                return true;
            }
            catch
            {
                // If checking status fails, assume it's ready so we don't block legitimate print jobs
                return true;
            }
        }

        private static List<(int Start, int End)> ParsePageRanges(string input, int maxPages)
        {
            var result = new List<(int, int)>();
            if (string.IsNullOrWhiteSpace(input))
            {
                result.Add((1, maxPages));
                return result;
            }
            
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Contains("-"))
                {
                    var bounds = p.Split('-');
                    if (bounds.Length == 2 && int.TryParse(bounds[0], out int start) && int.TryParse(bounds[1], out int end))
                    {
                        start = Math.Max(1, start);
                        end = Math.Min(maxPages, end);
                        if (start <= end) result.Add((start, end));
                    }
                }
                else if (int.TryParse(p, out int page))
                {
                    page = Math.Max(1, Math.Min(maxPages, page));
                    result.Add((page, page));
                }
            }
            
            if (result.Count == 0) result.Add((1, maxPages));
            return result;
        }
    }
}
