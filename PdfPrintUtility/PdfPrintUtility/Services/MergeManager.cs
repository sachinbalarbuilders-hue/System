using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfPrintUtility.Services
{
    public static class MergeManager
    {
        static MergeManager()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Merges the provided list of files into a single PDF. Ignores non-PDF files.
        /// </summary>
        public static async Task<(bool Success, string ErrorMessage, int FilesMerged)> MergePdfsAsync(List<Models.PrintFileItem> fileItems, string outputPath)
        {
            if (fileItems == null || fileItems.Count == 0)
                return (false, "No files provided to merge.", 0);

            var pdfFiles = fileItems.Where(f => f.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();

            if (pdfFiles.Count == 0)
                return (false, "None of the selected files were PDF documents. Merging images is currently not supported.", 0);

            return await Task.Run(() =>
            {
                try
                {
                    using (PdfDocument outPdf = new PdfDocument())
                    {
                        foreach (var item in pdfFiles)
                        {
                            if (!File.Exists(item.FilePath)) continue;

                            // Open the document to import pages from it
                            using (PdfDocument inPdf = PdfReader.Open(item.FilePath, PdfDocumentOpenMode.Import))
                            {
                                for (int i = 0; i < inPdf.PageCount; i++)
                                {
                                    var page = inPdf.Pages[i];
                                    if (item.Rotation != 0)
                                    {
                                        page.Rotate = (page.Rotate + item.Rotation) % 360;
                                    }
                                    outPdf.AddPage(page);
                                }
                            }
                        }

                        if (outPdf.PageCount == 0)
                            return (false, "No pages were found to merge.", 0);

                        outPdf.Save(outputPath);
                        return (true, string.Empty, pdfFiles.Count);
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"An error occurred while merging PDFs:\n\n{ex.Message}", 0);
                }
            });
        }

        /// <summary>
        /// Merges a list of specific individual pages into a single PDF, applying rotations.
        /// </summary>
        public static async Task<(bool Success, string ErrorMessage, int PagesMerged)> MergePagesAsync(List<Models.PdfPageItem> pages, string outputPath)
        {
            if (pages == null || pages.Count == 0)
                return (false, "No pages provided to merge.", 0);

            try
            {
                return await Task.Run(() =>
                {
                    using (PdfDocument outPdf = new PdfDocument())
                    {
                        // To optimize, we can cache open documents so we don't reopen the same file repeatedly
                        var openDocs = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);

                        try
                        {
                            foreach (var pageItem in pages)
                            {
                                if (!File.Exists(pageItem.SourceFilePath)) continue;

                                if (!openDocs.TryGetValue(pageItem.SourceFilePath, out PdfDocument inPdf))
                                {
                                    inPdf = PdfReader.Open(pageItem.SourceFilePath, PdfDocumentOpenMode.Import);
                                    openDocs[pageItem.SourceFilePath] = inPdf;
                                }

                                if (pageItem.SourcePageIndex >= 0 && pageItem.SourcePageIndex < inPdf.PageCount)
                                {
                                    var importedPage = inPdf.Pages[pageItem.SourcePageIndex];
                                    if (pageItem.Rotation != 0)
                                    {
                                        importedPage.Rotate = (importedPage.Rotate + pageItem.Rotation) % 360;
                                    }
                                    outPdf.AddPage(importedPage);
                                }
                            }

                            outPdf.Save(outputPath);
                        }
                        finally
                        {
                            foreach (var doc in openDocs.Values)
                            {
                                doc.Dispose();
                            }
                        }

                        return (true, string.Empty, pages.Count);
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }
    }
}
