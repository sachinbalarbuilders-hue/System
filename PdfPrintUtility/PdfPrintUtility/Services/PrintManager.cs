using System;
using System.Drawing.Printing;
using PdfiumViewer;
using PdfPrintUtility.Models;

namespace PdfPrintUtility.Services
{
    public static class PrintManager
    {
        public static bool PrintPdf(string filePath, PrintSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using var document = PdfDocument.Load(filePath);
                using var printDocument = document.CreatePrintDocument();

                printDocument.PrinterSettings.PrinterName = string.IsNullOrWhiteSpace(settings.DefaultPrinter) 
                    ? new PrinterSettings().PrinterName // Use system default
                    : settings.DefaultPrinter;

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

                // Apply Paper Size
                foreach (PaperSize size in printDocument.PrinterSettings.PaperSizes)
                {
                    if (size.PaperName.Equals(settings.PaperSize, StringComparison.OrdinalIgnoreCase))
                    {
                        printDocument.DefaultPageSettings.PaperSize = size;
                        break;
                    }
                }

                // Use StandardPrintController to prevent the print dialog/progress dialog from appearing
                printDocument.PrintController = new StandardPrintController();

                printDocument.Print();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
